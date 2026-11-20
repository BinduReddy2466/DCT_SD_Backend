using System.Text.Json;
using DCT_SD.Configuration;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Dashboard;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

// Aggregates existing per-RD data (OcrExtractionRecords, ManualValidationRequests,
// EmptyFolderRecords, FailedExtractionRecords, MigrationRecords/MigrationDocuments) into the
// two-row-per-RD (Folder / Images) statistics table the Dashboard displays. No new tables or
// columns - every count here is derived from data the app already tracks elsewhere.
//
// Activated has no existing data source anywhere in this app (MigrationRecord
// .TargetSystemActivationStatus is never written anywhere in this codebase - same external-API
// limitation as TargetSystemValidation elsewhere in this app) and is shown as 0 (Folder row) /
// N/A (Images row) by explicit product decision, rather than inventing a status/table for it or
// approximating it from unrelated data. WIP, Folder-level Failed Migration, and Images-level
// Failed Migration/Confirmed Duplicate DO have real sources (MigrationRecords.ProcessingStatus,
// MigrationDocuments.Status) and are wired below.
public class DashboardService : IDashboardService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(string? rdCode, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        // Ascending by RD Code (not Name) - per the Dashboard spec's explicit sort requirement.
        var registryOffices = await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new { c.Code, c.Name })
            .ToListAsync(cancellationToken);

        var ocrCounts = await _context.OcrExtractionRecords.AsNoTracking()
            .Where(r => r.RdCode != null)
            .GroupBy(r => r.RdCode!)
            .Select(g => new { RdCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RdCode, g => g.Count, cancellationToken);

        var emptyFolderCounts = await _context.EmptyFolderRecords.AsNoTracking()
            .Where(r => r.RdCode != null)
            .GroupBy(r => r.RdCode!)
            .Select(g => new { RdCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RdCode, g => g.Count, cancellationToken);

        var failedExtractionCounts = await _context.FailedExtractionRecords.AsNoTracking()
            .Where(r => r.RdCode != null)
            .GroupBy(r => r.RdCode!)
            .Select(g => new { RdCode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.RdCode, g => g.Count, cancellationToken);

        // ManualValidationStatus is a 3-way partition (every row is in exactly one) that lines
        // up with 3 dashboard columns: IncompleteExtraction -> "Incomplete Extraction",
        // TargetRdNotIdentified -> "Manual Validation" (still needs manual work before it can be
        // readied), ReadyForMigration -> "Ready for Migration". Grouping by (RdCode, Status)
        // keeps these mutually exclusive so Total never double-counts a folder.
        var mvStatusCounts = await _context.ManualValidationRequests.AsNoTracking()
            .Where(r => r.RdCode != null)
            .GroupBy(r => new { RdCode = r.RdCode!, r.Status })
            .Select(g => new { g.Key.RdCode, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var migratedFolderCounts = await _context.MigrationRecords.AsNoTracking()
            .Where(r => r.SdStatus == SupportingDocumentStatus.AllMigrated)
            .GroupBy(r => r.RdCode)
            .Select(g => new { RdCode = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // Folder-level WIP/Failed Migration - keyed off MigrationRecords.ProcessingStatus, the
        // same per-record claim/attempt status the Migration Monitoring list/details pages
        // already surface. "Failed Migration" groups Failed + RetryPending together, matching
        // MigrationService.ConsolidatedMigrationStatus's own existing "Failed Migration" bucket
        // (the same two raw statuses that list already presents under that one label) - this
        // Dashboard must not invent a second, inconsistent definition of "Failed Migration".
        var folderProcessingCounts = await _context.MigrationRecords.AsNoTracking()
            .Where(r => r.ProcessingStatus != null)
            .GroupBy(r => new { r.RdCode, r.ProcessingStatus })
            .Select(g => new { g.Key.RdCode, g.Key.ProcessingStatus, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // MigrationDocument (the "Images" entity) has no RdCode of its own - only reachable via
        // its parent MigrationRecord.RdCode - so this groups across the join rather than pulling
        // every document into memory.
        var docStatusCounts = await _context.MigrationDocuments.AsNoTracking()
            .GroupBy(d => new { d.MigrationRecord.RdCode, d.Status })
            .Select(g => new { g.Key.RdCode, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        // "Others" (excluded from migration) is only ever recorded per-document inside
        // ManualValidationRequests.DocumentsJson - there's no SQL-queryable column for it, so
        // this has to pull (RdCode, DocumentsJson) and count in-process. Selecting only these two
        // columns (not full entities) keeps the pulled payload as small as this data model allows.
        var othersCountsByRd = await CountOthersDocumentsByRdCodeAsync(cancellationToken);

        var allRows = new List<DashboardRowDto>();
        foreach (var rd in registryOffices)
        {
            var mvForRd = mvStatusCounts.Where(x => x.RdCode == rd.Code).ToList();
            int MvCount(ManualValidationStatus status) => mvForRd.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            var folderProcForRd = folderProcessingCounts.Where(x => x.RdCode == rd.Code).ToList();
            int FolderProcCount(MigrationProcessingStatus status) => folderProcForRd.FirstOrDefault(x => x.ProcessingStatus == status)?.Count ?? 0;

            var folderFetched = ocrCounts.GetValueOrDefault(rd.Code)
                + emptyFolderCounts.GetValueOrDefault(rd.Code)
                + failedExtractionCounts.GetValueOrDefault(rd.Code);
            var readyForMigration = MvCount(ManualValidationStatus.ReadyForMigration);
            var manualValidation = MvCount(ManualValidationStatus.TargetRdNotIdentified);
            var incompleteExtraction = MvCount(ManualValidationStatus.IncompleteExtraction);
            var migratedFolders = migratedFolderCounts.FirstOrDefault(x => x.RdCode == rd.Code)?.Count ?? 0;
            var emptyFolders = emptyFolderCounts.GetValueOrDefault(rd.Code);
            var folderWip = FolderProcCount(MigrationProcessingStatus.Processing);
            var folderFailedMigration = FolderProcCount(MigrationProcessingStatus.Failed) + FolderProcCount(MigrationProcessingStatus.RetryPending);

            allRows.Add(new DashboardRowDto
            {
                RdCode = rd.Code,
                RdName = rd.Name,
                RecordType = "Folder",
                Fetched = folderFetched,
                Wip = folderWip,
                ReadyForMigration = readyForMigration,
                ManualValidation = manualValidation,
                IncompleteExtraction = incompleteExtraction,
                Migrated = migratedFolders,
                FailedMigration = folderFailedMigration,
                EmptyFolders = emptyFolders,
                DuplicateSd = null,
                ConfirmedDuplicate = null,
                Inserted = null,
                NotApplicableOthers = null,
                Activated = 0,
                Total = readyForMigration + manualValidation + incompleteExtraction + migratedFolders + emptyFolders,
            });

            var docsForRd = docStatusCounts.Where(x => x.RdCode == rd.Code).ToList();
            int DocCount(MigrationDocumentStatus status) => docsForRd.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            var imagesFetched = docsForRd.Sum(x => x.Count);
            var migratedImages = DocCount(MigrationDocumentStatus.Migrated);
            var failedImages = DocCount(MigrationDocumentStatus.Failed);
            var duplicateSd = DocCount(MigrationDocumentStatus.DuplicateSd);
            var confirmedDuplicate = DocCount(MigrationDocumentStatus.ConfirmedDuplicate);
            var inserted = DocCount(MigrationDocumentStatus.InsertedAsNew);
            var imagesWip = DocCount(MigrationDocumentStatus.Pending);
            var others = othersCountsByRd.GetValueOrDefault(rd.Code);

            allRows.Add(new DashboardRowDto
            {
                RdCode = rd.Code,
                RdName = rd.Name,
                RecordType = "Images",
                Fetched = imagesFetched,
                Wip = imagesWip,
                ReadyForMigration = null,
                ManualValidation = null,
                IncompleteExtraction = null,
                Migrated = migratedImages,
                FailedMigration = failedImages,
                EmptyFolders = null,
                DuplicateSd = duplicateSd,
                ConfirmedDuplicate = confirmedDuplicate,
                Inserted = inserted,
                NotApplicableOthers = others,
                Activated = null,
                Total = migratedImages + duplicateSd + inserted + others,
            });
        }

        // The RD Code filter is an exact match against the dropdown/search box's committed
        // value (the spec's own "matching RD Code from the list based on the entered value" is
        // the dropdown's own type-ahead search, a client-side concern - once a value is actually
        // submitted, it's one specific RD Code), applied only to the paged Table below, never to
        // AllRows (the KPI cards/charts always summarize every RD regardless of this filter).
        var filteredRegistryOffices = string.IsNullOrWhiteSpace(rdCode)
            ? registryOffices
            : registryOffices.Where(r => string.Equals(r.Code, rdCode, StringComparison.OrdinalIgnoreCase)).ToList();

        var totalRdCount = filteredRegistryOffices.Count;
        var safePageNumber = pageNumber < 1 ? 1 : pageNumber;
        var safePageSize = pageSize is 10 or 25 or 50 or 100 ? pageSize : 25;
        var pagedCodes = filteredRegistryOffices
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .Select(r => r.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pagedRows = allRows.Where(r => pagedCodes.Contains(r.RdCode)).ToList();

        return new DashboardStatsDto
        {
            AllRows = allRows,
            Table = new PagedResult<DashboardRowDto>
            {
                Items = pagedRows,
                TotalCount = totalRdCount,
                PageNumber = safePageNumber,
                PageSize = safePageSize,
            },
        };
    }

    private async Task<Dictionary<string, int>> CountOthersDocumentsByRdCodeAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.ManualValidationRequests.AsNoTracking()
            .Where(r => r.RdCode != null && r.DocumentsJson != null)
            .Select(r => new { RdCode = r.RdCode!, r.DocumentsJson })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            var items = JsonSerializer.Deserialize<List<DocumentJsonItem>>(row.DocumentsJson!, JsonOptions) ?? [];
            var othersCount = items.Count(i => string.Equals(i.DocumentTypeCode, "OTHERS", StringComparison.OrdinalIgnoreCase));
            if (othersCount == 0) continue;

            result[row.RdCode] = result.GetValueOrDefault(row.RdCode) + othersCount;
        }

        return result;
    }

    private class DocumentJsonItem
    {
        public string? DocumentTypeCode { get; set; }
    }
}
