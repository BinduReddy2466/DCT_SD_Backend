using DCT_SD.Models.Dtos.Menus;

namespace DCT_SD.Services;

public interface IMenuService
{
    Task<IReadOnlyList<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default);
}
