using DCT_SD.Configuration;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

// Mirrors RegistryOfficeService exactly, for the "Document Type" side of the Manual Validation
// Supporting Documents manual-correction dropdown - same CodeLookups table, different
// LookupType discriminator, no new table/column.
public class DocumentTypeService : IDocumentTypeService
{
    private readonly ApplicationDbContext _context;

    public DocumentTypeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DocumentTypeDto>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
        await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.DocumentType && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new DocumentTypeDto { Id = c.Id, Code = c.Code, Name = c.Name })
            .ToListAsync(cancellationToken);
}
