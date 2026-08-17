using DCT_SD.Configuration;
using DCT_SD.Models.Dtos.RdConfig;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class RegistryOfficeService : IRegistryOfficeService
{
    private readonly ApplicationDbContext _context;

    public RegistryOfficeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RegistryOfficeDto>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.RegistryOffices.AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new RegistryOfficeDto { Id = o.Id, Code = o.Code, Name = o.Name })
            .ToListAsync(cancellationToken);
}
