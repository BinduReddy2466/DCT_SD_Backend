using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Roles;
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
    private readonly IRoleService _roleService;
    private readonly IMenuService _menuService;

    public UserService(ApplicationDbContext context, ICurrentUserService currentUser, IAuthService authService, IRoleService roleService, IMenuService menuService)
    {
        _context = context;
        _currentUser = currentUser;
        _authService = authService;
        _roleService = roleService;
        _menuService = menuService;
    }

    public async Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var rawTerm = request.SearchTerm.Trim();
            var term = rawTerm.ToLower();
            var isIdMatch = int.TryParse(rawTerm, out var idTerm);

            // Role and Status already have their own dedicated dropdown filters, so this box
            // is scoped to ID/Name/Username only - matching the search placeholder text.
            query = query.Where(u =>
                (isIdMatch && u.Id == idTerm) ||
                u.FirstName.ToLower().Contains(term) ||
                u.LastName.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term));
        }

        if (request.RoleId.HasValue)
        {
            var role = await _roleService.GetByIdAsync(request.RoleId.Value, cancellationToken);
            query = query.Where(u => u.RoleName == role.Name);
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            query = query.Where(u => u.Status == status);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(u => u.CreatedAt >= DateRangeFilter.StartOfDayUtc(request.DateFrom.Value));
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(u => u.CreatedAt < DateRangeFilter.EndOfDayExclusive(request.DateTo.Value));
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
        return await MapToDetailAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var trimmed = username.Trim();
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == trimmed.ToLower(), cancellationToken);

        return user is null ? null : await MapToDetailAsync(user, cancellationToken);
    }

    public async Task<UserDetailDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var role = await _roleService.GetByIdAsync(request.RoleId, cancellationToken);

        EnsureRoleAssignmentIsAllowed(role, currentRoleOfTargetUser: null);

        var username = request.Username.Trim();
        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), cancellationToken))
        {
            throw new ConflictException("This username already exists. Please choose a different username.");
        }

        var menuKeys = ResolveAssignedMenuKeys(role, request.AssignedMenuIds);

        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(request.Password),
            RoleName = role.Name,
            MenuPermissionsCsv = menuKeys.Count == 0 ? null : string.Join(',', menuKeys),
            Status = UserStatus.Active,
            // The admin just chose this password for them - they must change it on first login.
            MustChangePassword = true,
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueUsernameViolation(ex))
        {
            // The check above and this insert aren't atomic - two requests close together
            // (e.g. a double-clicked Save button) can both pass the check before either
            // commits. The database's own unique constraint is what actually catches that
            // case, so translate it into the same friendly message instead of letting the
            // raw SqlException surface as an unhandled 500.
            throw new ConflictException("This username already exists. Please choose a different username.");
        }

        return await MapToDetailAsync(user, cancellationToken);
    }

    private static bool IsUniqueUsernameViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 } sqlEx &&
        sqlEx.Message.Contains("UK_Users_01", StringComparison.OrdinalIgnoreCase);

    public async Task<UpdateUserResult> UpdateAsync(int id, UpdateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        // Captured before anything below mutates it - the only current source of the user's
        // pre-update status, so the caller no longer needs its own separate GetByIdAsync just to
        // learn this. EffectiveStatus, not the raw enum, so this matches exactly what a
        // GetByIdAsync call would have reported (e.g. an elapsed auto-lock reads as Active here
        // too, not as the still-stored Locked value).
        var previousStatus = EffectiveStatus(user).ToString();

        // The Edit button is hidden entirely for an Administrator-role row (and for a Sub-Admin
        // viewer looking at a Sub-Admin row below), but that's only a UI hint - a crafted request
        // could still post directly to this endpoint.
        if (user.RoleName == RoleNames.Administrator)
        {
            throw new ForbiddenAppException("The Admin account cannot be edited.");
        }

        if (_currentUser.Role == RoleNames.SubAdmin && user.RoleName == RoleNames.SubAdmin)
        {
            throw new ForbiddenAppException("A Sub-Admin cannot edit a Sub-Admin account.");
        }

        var role = await _roleService.GetByIdAsync(request.RoleId, cancellationToken);

        EnsureRoleAssignmentIsAllowed(role, currentRoleOfTargetUser: user.RoleName);

        // The role dropdown is disabled client-side for a Sub-Admin account's row - its Role can
        // never be changed through this form - but a disabled field is only a UI hint; a crafted
        // request could still post a different RoleId. (An Administrator row never reaches this
        // point at all - it's already rejected above.)
        if (user.RoleName == RoleNames.SubAdmin && role.Name != user.RoleName)
        {
            throw new ForbiddenAppException("The role of a Sub-Admin account cannot be changed.");
        }

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            throw new BusinessValidationException("Status must be one of: Active, Deactivated, Locked.");
        }

        var menuKeys = ResolveAssignedMenuKeys(role, request.AssignedMenuIds);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.RoleName = role.Name;
        user.Status = status;
        user.MenuPermissionsCsv = menuKeys.Count == 0 ? null : string.Join(',', menuKeys);

        await _context.SaveChangesAsync(cancellationToken);

        // A move out of Active status must not leave the user's existing sessions usable - the
        // access token already issued still works for up to its own short lifetime, but no
        // further silent refresh will succeed once these are revoked.
        if (status != UserStatus.Active)
        {
            await _authService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
        }

        return new UpdateUserResult(await MapToDetailAsync(user, cancellationToken), previousStatus);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        if (_currentUser.UserId == id)
        {
            throw new ForbiddenAppException("You cannot delete your own account.");
        }

        // The Delete button is hidden entirely for Administrator rows, but that's only a UI
        // hint - enforce it here too. Administrator accounts aren't deletable through User
        // Management at all, regardless of how many other Administrators exist.
        if (user.RoleName == RoleNames.Administrator)
        {
            throw new ForbiddenAppException("Administrator accounts cannot be deleted through User Management.");
        }

        // Likewise, a Sub-Admin viewer can't delete any Sub-Admin account - another one's, or
        // their own (the self-delete check above already covers that specific case, but this
        // also blocks a Sub-Admin from deleting a *different* Sub-Admin).
        if (_currentUser.Role == RoleNames.SubAdmin && user.RoleName == RoleNames.SubAdmin)
        {
            throw new ForbiddenAppException("A Sub-Admin cannot delete a Sub-Admin account.");
        }

        // RefreshTokens.UserId -> Users.Id is a real FK with NO_ACTION, so those rows have to
        // go before the Users row itself can be removed.
        var tokens = await _context.RefreshTokens.Where(t => t.UserId == id).ToListAsync(cancellationToken);
        _context.RefreshTokens.RemoveRange(tokens);
        _context.Users.Remove(user);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(int userId, string newPassword, bool requireChangeOnNextLogin, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = requireChangeOnNextLogin;
        await _context.SaveChangesAsync(cancellationToken);

        // Mirrors UpdateAsync's status-change handling - a password reset must invalidate any
        // refresh tokens already issued under the old password, so a session started before the
        // reset can't silently keep renewing afterward.
        await _authService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
    }

    public async Task ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await LoadUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        if (!PasswordHasher.Verify(oldPassword, user.PasswordHash))
        {
            throw new BusinessValidationException("The old password you entered is incorrect.");
        }

        if (PasswordHasher.Verify(newPassword, user.PasswordHash))
        {
            throw new BusinessValidationException("The new password must be different from your old password.");
        }

        user.PasswordHash = PasswordHasher.Hash(newPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync(cancellationToken);

        // Same reasoning as ResetPasswordAsync - a password change must invalidate any refresh
        // tokens already issued under the old password.
        await _authService.RevokeAllRefreshTokensForUserAsync(user.Id, cancellationToken);
    }

    private Task<User?> LoadUserAsync(int id, CancellationToken cancellationToken) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    private void EnsureRoleAssignmentIsAllowed(RoleDto role, string? currentRoleOfTargetUser)
    {
        // Administrator is never a selectable option on create, and the role dropdown is
        // disabled while editing an existing Administrator's own account, so this only fires
        // for a genuine attempt to newly assign Administrator - not for a resubmission of an
        // Administrator account that already had that role and isn't changing it.
        if (role.Name == RoleNames.Administrator && role.Name != currentRoleOfTargetUser)
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

    private IReadOnlyList<string> ResolveAssignedMenuKeys(RoleDto role, IReadOnlyCollection<int> requestedMenuIds)
    {
        if (role.Name != RoleNames.SubAdmin)
        {
            return Array.Empty<string>();
        }

        if (requestedMenuIds.Count == 0)
        {
            throw new BusinessValidationException("Please assign a tab to the user before proceeding.");
        }

        var distinctIds = requestedMenuIds.Distinct().ToList();
        var keys = _menuService.ResolveKeys(distinctIds);
        if (keys.Count != distinctIds.Count)
        {
            throw new BusinessValidationException("One or more assigned menus are invalid.");
        }

        return keys;
    }

    private static UserListItemDto MapToListItem(User user) => new()
    {
        Id = user.Id,
        DateCreated = user.CreatedAt,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Username = user.Username,
        Role = user.RoleName,
        Status = EffectiveStatus(user).ToString(),
    };

    // An auto-lock (LockoutEndUtc set) that has already elapsed should read as Active here even
    // if nobody has logged in yet to trigger AuthService.LoginAsync's own lazy clear of the
    // Status/LockoutEndUtc columns - mirrors the same lazy-expiry read pattern already used for
    // Manual Validation record locks (a stale lock isn't eagerly cleared from the DB either, it's
    // just treated as expired wherever it's read) rather than needing a background sweep job.
    private static UserStatus EffectiveStatus(User user) =>
        user.Status == UserStatus.Locked && user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value <= DateTime.UtcNow
            ? UserStatus.Active
            : user.Status;

    private async Task<UserDetailDto> MapToDetailAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetAllAsync(cancellationToken);
        var roleId = roles.FirstOrDefault(r => r.Name == user.RoleName)?.Id ?? 0;

        // Only a current Sub-Admin has real, explicit menu grants. Other roles can still carry
        // a leftover/legacy MenuPermissionsCsv value (e.g. from seeding) that has nothing to do
        // with an intentional Sub-Admin assignment - resolving it here would silently pre-check
        // menus in the Assign Tab picker that the admin never chose for this account.
        var menuKeys = user.RoleName == RoleNames.SubAdmin && !string.IsNullOrWhiteSpace(user.MenuPermissionsCsv)
            ? user.MenuPermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        return new UserDetailDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            PasswordHash = user.PasswordHash,
            RoleId = roleId,
            Role = user.RoleName,
            Status = EffectiveStatus(user).ToString(),
            AssignedMenuIds = _menuService.ResolveIds(menuKeys),
            DateCreated = user.CreatedAt,
        };
    }
}
