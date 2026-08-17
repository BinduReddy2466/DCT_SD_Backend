using DCT_SD.Configuration;
using DCT_SD.Models.Dtos.Menus;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class MenuService : IMenuService
{
    private readonly ApplicationDbContext _context;

    public MenuService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<MenuDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Menus.AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new MenuDto { Id = m.Id, Key = m.Key, Label = m.Label, IsBaseMenu = m.IsBaseMenu })
            .ToListAsync(cancellationToken);
}
