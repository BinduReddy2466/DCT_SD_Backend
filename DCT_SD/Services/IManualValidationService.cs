using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;

namespace DCT_SD.Services;

public interface IManualValidationService
{
    // excludeReadyForMigration: defaults to false so every existing caller (Reports included)
    // keeps seeing the full Manual Validation history exactly as before. The Manual Validation
    // list page itself passes true, since a record marked Ready for Migration is now queued for
    // migration rather than pending manual validation - MarkReadyForMigrationAsync below.
    Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, bool excludeReadyForMigration = false, CancellationToken cancellationToken = default);

    /// Same filters/grouping as SearchAsync, but returns every matching (grouped) row in one call
    /// instead of one SearchAsync-sized page at a time - for Reports' full-table Excel export,
    /// which previously paged through SearchAsync (capped at 100 rows/call) and, because
    /// SearchAsync redoes the full filter+group pass on every call regardless of page size,
    /// re-loaded and re-grouped the entire matching set once per 100-row page (O(n^2) for n
    /// matching rows). This method does that same load+group pass exactly once.
    Task<List<ManualValidationListItemDto>> SearchAllAsync(ManualValidationSearchRequestDto request, bool excludeReadyForMigration = false, CancellationToken cancellationToken = default);
    Task<PagedResult<ManualValidationRemarkDto>> GetRemarksHistoryAsync(int id, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default);
    Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default);
    Task ReleaseLocksForUserAsync(int userId, CancellationToken cancellationToken = default);

    // Replaces the old "Migrate" action: marks every sibling in the Entry Number group Status as
    // ReadyForMigration, records it in Action History, and ensures one MigrationRecords row exists
    // per (sibling x split Entry Number Range value) combination (MigrationType=ManualValidation)
    // so each becomes visible on Migration Monitoring. Idempotent - safe to call on every click,
    // only the combinations that don't exist yet are created. Never sets MigratedAt itself here -
    // that's stamped later, per sibling, by MigrationService once every one of that sibling's
    // combinations has genuinely finished migrating (see
    // MarkManualValidationRequestMigratedIfCompleteAsync). Returns the id of the single next
    // not-yet-attempted combination to initiate in the background (null if every combination
    // already exists and has been attempted) and whether it's the last one remaining (client
    // requirement, 2026-11-27/28: one button, clicked repeatedly, advances exactly one combination
    // per click - combinations are never displayed individually on this page - and the caller
    // redirects away once the last one has been queued, instead of staying on this page).
    Task<(int? NextEligibleId, bool IsLastCombination)> MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default);
    Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default);

    // Persists a Title Sequence value the user picked from the "Repeating Title Number" modal
    // (the single-match path inside RetrieveTitleSequenceAsync persists through the same
    // mechanism internally) - recordId identifies the specific Title Record row, never guessed
    // by position.
    Task SelectTitleSequenceAsync(int recordId, string sequence, CancellationToken cancellationToken = default);

    /// Looks up the imagePath stored in DocumentsJson for the document at the given 1-based
    /// position (the same ordering/position used by ManualValidationDocumentDto.Id from
    /// OpenForEditAsync's Documents list). Returns null if the record, or a document at that
    /// position, doesn't exist.
    Task<string?> GetDocumentImagePathAsync(int id, int documentId, CancellationToken cancellationToken = default);
}
