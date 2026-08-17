using DCT_SD.Models;
using DCT_SD.Models.Dtos.Users;

namespace DCT_SD.Services;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<UserDetailDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDetailDto> UpdateAsync(int id, UpdateUserRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
