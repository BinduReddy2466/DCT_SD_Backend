using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Services;

public interface IManualValidationService
{
    Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, CancellationToken cancellationToken = default);

    /// Same filters/grouping as SearchAsync, but returns every matching (grouped) row in one call
    /// instead of one SearchAsync-sized page at a time - for Reports' full-table Excel export,
    /// which previously paged through SearchAsync (capped at 100 rows/call) and, because
    /// SearchAsync redoes the full filter+group pass on every call regardless of page size,
    /// re-loaded and re-grouped the entire matching set once per 100-row page (O(n^2) for n
    /// matching rows). This method does that same load+group pass exactly once.
    Task<List<ManualValidationListItemDto>> SearchAllAsync(ManualValidationSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<ManualValidationRemarkDto>> GetRemarksHistoryAsync(int id, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default);
    Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default);
    Task ReleaseLocksForUserAsync(int userId, CancellationToken cancellationToken = default);

    // Replaces the old "Migrate" action: only marks the record Status as ReadyForMigration and
    // records it in Action History - it never sets MigratedAt itself and never talks to
    // MigrationRecords/Migration Monitoring, which stay entirely unaffected. The record remains
    // fully visible/editable in Manual Validation afterward; the actual migration mechanism
    // (external to this app) is expected to pick up ReadyForMigration records on its own.
    Task MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default);
    Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default);

    /// Looks up the imagePath stored in DocumentsJson for the document at the given 1-based
    /// position (the same ordering/position used by ManualValidationDocumentDto.Id from
    /// OpenForEditAsync's Documents list). Returns null if the record, or a document at that
    /// position, doesn't exist.
    Task<string?> GetDocumentImagePathAsync(int id, int documentId, CancellationToken cancellationToken = default);
}
