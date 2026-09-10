using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Services;

public interface IDocumentTypeService
{
    Task<IReadOnlyList<DocumentTypeDto>> GetAllActiveAsync(CancellationToken cancellationToken = default);
}
