using DCT_SD.Configuration;
using DCT_SD.Models;
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
        await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new RegistryOfficeDto { Id = c.Id, Code = c.Code, Name = c.Name })
            .ToListAsync(cancellationToken);
}
