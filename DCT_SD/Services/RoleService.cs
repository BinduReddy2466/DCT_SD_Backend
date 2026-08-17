using DCT_SD.Configuration;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models.Dtos.Roles;
using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _context;

    public RoleService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _context.Roles.AsNoTracking().OrderBy(r => r.Id).ToListAsync(cancellationToken))
            .Select(MapToDto).ToArray();

    public async Task<RoleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(nameof(Role), id);
        return MapToDto(role);
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (await _context.Roles.AnyAsync(r => r.Name.ToLower() == name.ToLower(), cancellationToken))
        {
            throw new ConflictException($"Role '{name}' already exists.");
        }

        var role = new Role { Name = name, Description = request.Description?.Trim(), IsSystemDefined = false };
        _context.Roles.Add(role);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(role);
    }

    public async Task<RoleDto> UpdateAsync(int id, UpdateRoleRequestDto request, CancellationToken cancellationToken = default)
    {
        var role = await _context.Roles.FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(nameof(Role), id);

        var newName = request.Name.Trim();

        if (role.IsSystemDefined && !string.Equals(role.Name, newName, StringComparison.Ordinal))
        {
            throw new ForbiddenAppException("System-defined roles cannot be renamed.");
        }

        if (await _context.Roles.AnyAsync(r => r.Id != id && r.Name.ToLower() == newName.ToLower(), cancellationToken))
        {
            throw new ConflictException($"Role '{newName}' already exists.");
        }

        role.Name = newName;
        role.Description = request.Description?.Trim();
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(role);
    }

    private static RoleDto MapToDto(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        IsSystemDefined = role.IsSystemDefined,
    };
}
