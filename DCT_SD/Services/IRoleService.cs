using DCT_SD.Models.Dtos.Roles;

namespace DCT_SD.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateAsync(CreateRoleRequestDto request, CancellationToken cancellationToken = default);
    Task<RoleDto> UpdateAsync(int id, UpdateRoleRequestDto request, CancellationToken cancellationToken = default);
}
