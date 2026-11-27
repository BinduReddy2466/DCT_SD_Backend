using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;

namespace DCT_SD.Services;

public interface IMigrationService
{
    Task<PagedResult<MigrationListItemDto>> SearchAsync(MigrationSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task InitiateMigrationAsync(int id, CancellationToken cancellationToken = default);

    // Processes a batch of newly-created MigrationRecord ids (one per entry number) sequentially,
    // one InitiateMigrationAsync call at a time, awaiting each before starting the next. Called
    // from a background work item right after Manual Validation's "Ready for Migration" confirm
    // creates these rows - see MigrationService's own XML comment for the full rationale.
    Task ProcessEntryNumbersSequentiallyAsync(IReadOnlyList<int> migrationRecordIds, CancellationToken cancellationToken = default);
    Task<string?> GetDocumentImagePathAsync(int migrationId, int documentId, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> InsertAsNewDocumentAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> ConfirmDuplicateAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> SaveCorrectionsAsync(int id, SaveMigrationCorrectionsRequestDto request, CancellationToken cancellationToken = default);
    Task MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default);

    // Re-triggers migration for an existing Migration Monitoring row (distinct from
    // MarkReadyForMigrationAsync above) - see MigrationService's own XML comment.
    Task TriggerReadyForMigrationAsync(int id, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> ReprocessAsync(int id, string remarks, CancellationToken cancellationToken = default);
}
