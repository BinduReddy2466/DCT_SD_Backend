using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;

namespace DCT_SD.Services;

public interface IMigrationService
{
    Task<PagedResult<MigrationListItemDto>> SearchAsync(MigrationSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task InitiateMigrationAsync(int id, CancellationToken cancellationToken = default);
    Task<string?> GetDocumentImagePathAsync(int migrationId, int documentId, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> InsertAsNewDocumentAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> ConfirmDuplicateAsync(int migrationId, int documentId, string remarks, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> SaveCorrectionsAsync(int id, SaveMigrationCorrectionsRequestDto request, CancellationToken cancellationToken = default);
    Task MarkReadyForMigrationAsync(int id, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> ReprocessAsync(int id, string remarks, CancellationToken cancellationToken = default);
}
