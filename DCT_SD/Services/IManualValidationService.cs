using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Services;

public interface IManualValidationService
{
    Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<ManualValidationRemarkDto>> GetRemarksHistoryAsync(int id, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default);
    Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default);
    Task MigrateAsync(int id, CancellationToken cancellationToken = default);
    Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default);
}
