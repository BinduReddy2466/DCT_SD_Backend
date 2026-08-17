using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Users;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthService _authService;

    public UserService(ApplicationDbContext context, ICurrentUserService currentUser, IAuthService authService)
    {
        _context = context;
        _currentUser = currentUser;
        _authService = authService;
    }

    public async Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().Include(u => u.Role).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term) ||
                u.Role.Name.ToLower().Contains(term));
        }

        if (request.RoleId.HasValue)
        {
            query = query.Where(u => u.RoleId == request.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(u => u.CreatedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(u => u.CreatedAt <= request.DateTo.Value);
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItemDto>
        {
            Items = items.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<UserDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);
        return MapToDetail(user);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync([request.RoleId], cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        EnsureRoleAssignmentIsAllowed(role, currentRoleOfTargetUser: null);

        var username = request.Username.Trim();
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken))
        {
            throw new ConflictException($"Username '{username}' is already in use.");
        }

        var menuIds = await ResolveAssignedMenuIdsAsync(role, request.AssignedMenuIds, cancellationToken);

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(request.Password),
            RoleId = role.Id,
            Status = UserStatus.Active,
        };

        foreach (var menuId in menuIds)
        {
            user.MenuPermissions.Add(new UserMenuPermission { MenuId = menuId, GrantedAt = DateTime.UtcNow });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        user.Role = role;
        return MapToDetail(user);
    }

    public async Task<UserDetailDto> UpdateAsync(int id, UpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        var role = await _context.Roles.FindAsync([request.RoleId], cancellationToken)
            ?? throw new NotFoundException(nameof(Role), request.RoleId);

        EnsureRoleAssignmentIsAllowed(role, currentRoleOfTargetUser: user.Role.Name);

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            throw new BusinessValidationException("Status must be one of: Active, Deactivated, Locked.");
        }

        if (user.RoleId == role.Id && user.Role.Name == RoleNames.Administrator
            && status != UserStatus.Active
            && await IsLastActiveAdministratorAsync(user.Id, cancellationToken))
        {
            throw new BusinessValidationException("At least one active Administrator account must remain.");
        }

        var menuIds = await ResolveAssignedMenuIdsAsync(role, request.AssignedMenuIds, cancellationToken);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.RoleId = role.Id;
        user.Status = status;

        var passwordChanged = !string.IsNullOrWhiteSpace(request.Password);
        if (passwordChanged)
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password!);
            user.FailedLoginAttempts = 0;
        }

        user.MenuPermissions.Clear();
        foreach (var menuId in menuIds)
        {
            user.MenuPermissions.Add(new UserMenuPermission { UserId = user.Id, MenuId = menuId, GrantedAt = DateTime.UtcNow });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // A password reset or a move out of Active status must not leave the user's existing
        // sessions usable - the access token already issued still works for up to its own
        // short lifetime, but no further silent refresh will succeed once these are revoked.
        if (passwordChanged || status != UserStatus.Active)
        {
            await _authService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
        }

        user.Role = role;
        return MapToDetail(user);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        if (_currentUser.UserId == id)
        {
            throw new ForbiddenAppException("You cannot delete your own account.");
        }

        if (user.Role.Name == RoleNames.Administrator && await IsLastActiveAdministratorAsync(id, cancellationToken))
        {
            throw new BusinessValidationException("At least one active Administrator account must remain.");
        }

        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.Status = UserStatus.Deactivated;

        await _context.SaveChangesAsync(cancellationToken);
        await _authService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
    }

    private Task<User?> LoadUserAsync(int id, CancellationToken cancellationToken) =>
        _context.Users
            .Include(u => u.Role)
            .Include(u => u.MenuPermissions).ThenInclude(p => p.Menu)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    private void EnsureRoleAssignmentIsAllowed(Role role, string? currentRoleOfTargetUser)
    {
        if (role.Name == RoleNames.Administrator)
        {
            throw new ForbiddenAppException("Administrator accounts cannot be created or assigned through this interface.");
        }

        var isSubAdminAssignment = role.Name == RoleNames.SubAdmin;
        var wasAlreadySubAdmin = currentRoleOfTargetUser == RoleNames.SubAdmin;

        if (isSubAdminAssignment && !wasAlreadySubAdmin && _currentUser.Role == RoleNames.SubAdmin)
        {
            throw new ForbiddenAppException("Only an Administrator can assign the Sub-Admin role.");
        }
    }

    private async Task<List<int>> ResolveAssignedMenuIdsAsync(Role role, IReadOnlyCollection<int> requestedMenuIds, CancellationToken cancellationToken)
    {
        if (role.Name != RoleNames.SubAdmin)
        {
            return new List<int>();
        }

        if (requestedMenuIds.Count == 0)
        {
            throw new BusinessValidationException("At least one menu must be assigned to a Sub-Admin account.");
        }

        var distinctIds = requestedMenuIds.Distinct().ToList();
        var menus = await _context.Menus.Where(m => distinctIds.Contains(m.Id)).ToListAsync(cancellationToken);
        if (menus.Count != distinctIds.Count)
        {
            throw new BusinessValidationException("One or more assigned menus are invalid.");
        }

        return menus.Select(m => m.Id).ToList();
    }

    private async Task<bool> IsLastActiveAdministratorAsync(int excludingUserId, CancellationToken cancellationToken)
    {
        var administratorRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Administrator, cancellationToken);
        if (administratorRole is null)
        {
            return false;
        }

        var remainingActiveAdmins = await _context.Users
            .Where(u => u.RoleId == administratorRole.Id && u.Status == UserStatus.Active && u.Id != excludingUserId)
            .CountAsync(cancellationToken);

        return remainingActiveAdmins == 0;
    }

    private static UserListItemDto MapToListItem(User user) => new()
    {
        Id = user.Id,
        DateCreated = user.CreatedAt,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Username = user.Username,
        Role = user.Role.Name,
        Status = user.Status.ToString(),
    };

    private static UserDetailDto MapToDetail(User user) => new()
    {
        Id = user.Id,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Username = user.Username,
        RoleId = user.RoleId,
        Role = user.Role.Name,
        Status = user.Status.ToString(),
        AssignedMenuIds = user.MenuPermissions.Select(p => p.MenuId).ToArray(),
        DateCreated = user.CreatedAt,
    };
}
