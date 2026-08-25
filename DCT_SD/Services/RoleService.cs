using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Roles;

namespace DCT_SD.Services;

// PROVISIONAL: the new DCT_SD schema has no Roles table - Users.RoleName is a free-text
// column, so custom role CRUD has no backing store anymore. This exposes the fixed set of
// role names already found in the live Users data (RoleNames) as a read-only list so the
// User form's role dropdown keeps working; Create/Update are disabled rather than faking
// persistence. Screens 17-22 (User Management) are flagged out of scope pending explicit
// direction - this stub only keeps the solution compiling and the read path functional.
public class RoleService : IRoleService
{
    private static readonly RoleDto[] FixedRoles =
    [
        new() { Id = 1, Name = RoleNames.Administrator, Description = "Full system access.", IsSystemDefined = true },
        new() { Id = 2, Name = RoleNames.SubAdmin, Description = "Administrator-delegated access to explicitly assigned modules.", IsSystemDefined = true },
        new() { Id = 3, Name = RoleNames.Encoder, Description = "Operational data-entry access to the core pipeline modules.", IsSystemDefined = false },
        new() { Id = 4, Name = RoleNames.LaresQa, Description = "LARES quality-assurance review access.", IsSystemDefined = false },
        new() { Id = 5, Name = RoleNames.LraQa, Description = "LRA quality-assurance review access.", IsSystemDefined = false },
    ];

    public Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleDto>>(FixedRoles);

    public Task<RoleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var role = FixedRoles.FirstOrDefault(r => r.Id == id)
            ?? throw new NotFoundException("Role", id);
        return Task.FromResult(role);
    }

    public Task<RoleDto> CreateAsync(CreateRoleRequestDto request, CancellationToken cancellationToken = default) =>
        throw new BusinessValidationException("Custom roles are not supported: the current database has no Roles table (Users.RoleName is a fixed value).");

    public Task<RoleDto> UpdateAsync(int id, UpdateRoleRequestDto request, CancellationToken cancellationToken = default) =>
        throw new BusinessValidationException("Roles cannot be edited: the current database has no Roles table (Users.RoleName is a fixed value).");
}
