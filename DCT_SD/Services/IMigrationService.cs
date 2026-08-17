using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;

namespace DCT_SD.Services;

public interface IMigrationService
{
    Task<PagedResult<MigrationListItemDto>> SearchAsync(MigrationSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> OverwriteDocumentAsync(int migrationId, int documentId, CancellationToken cancellationToken = default);
    Task<MigrationDetailDto> InsertAsNewDocumentAsync(int migrationId, int documentId, CancellationToken cancellationToken = default);
}
