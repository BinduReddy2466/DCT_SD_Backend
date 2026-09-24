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
// Three columns (WIP, Failed Migration, Confirmed Duplicate) have no existing data source
// anywhere in this app (confirmed by a full-codebase inspection before this was built) and are
// shown as 0 by explicit product decision, rather than inventing a status/table for them or
// approximating them from unrelated data.
public class DashboardService : IDashboardService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;

    public DashboardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DashboardRowDto>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var registryOffices = await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.IsActive)
            .OrderBy(c => c.Name)
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

        var rows = new List<DashboardRowDto>();
        foreach (var rd in registryOffices)
        {
            var mvForRd = mvStatusCounts.Where(x => x.RdCode == rd.Code).ToList();
            int MvCount(ManualValidationStatus status) => mvForRd.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            var folderFetched = ocrCounts.GetValueOrDefault(rd.Code)
                + emptyFolderCounts.GetValueOrDefault(rd.Code)
                + failedExtractionCounts.GetValueOrDefault(rd.Code);
            var readyForMigration = MvCount(ManualValidationStatus.ReadyForMigration);
            var manualValidation = MvCount(ManualValidationStatus.TargetRdNotIdentified);
            var incompleteExtraction = MvCount(ManualValidationStatus.IncompleteExtraction);
            var migratedFolders = migratedFolderCounts.FirstOrDefault(x => x.RdCode == rd.Code)?.Count ?? 0;
            var emptyFolders = emptyFolderCounts.GetValueOrDefault(rd.Code);

            rows.Add(new DashboardRowDto
            {
                RdCode = rd.Code,
                RdName = rd.Name,
                RecordType = "Folder",
                Fetched = folderFetched,
                Wip = 0,
                ReadyForMigration = readyForMigration,
                ManualValidation = manualValidation,
                IncompleteExtraction = incompleteExtraction,
                Migrated = migratedFolders,
                FailedMigration = 0,
                EmptyFolders = emptyFolders,
                DuplicateSd = null,
                ConfirmedDuplicate = null,
                Inserted = null,
                NotApplicableOthers = null,
                Total = readyForMigration + manualValidation + incompleteExtraction + migratedFolders + emptyFolders,
            });

            var docsForRd = docStatusCounts.Where(x => x.RdCode == rd.Code).ToList();
            int DocCount(MigrationDocumentStatus status) => docsForRd.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            var imagesFetched = docsForRd.Sum(x => x.Count);
            var migratedImages = DocCount(MigrationDocumentStatus.Migrated);
            var duplicateSd = DocCount(MigrationDocumentStatus.DuplicateSd);
            var inserted = DocCount(MigrationDocumentStatus.InsertedAsNew);
            var others = othersCountsByRd.GetValueOrDefault(rd.Code);

            rows.Add(new DashboardRowDto
            {
                RdCode = rd.Code,
                RdName = rd.Name,
                RecordType = "Images",
                Fetched = imagesFetched,
                Wip = 0,
                ReadyForMigration = null,
                ManualValidation = null,
                IncompleteExtraction = null,
                Migrated = migratedImages,
                FailedMigration = 0,
                EmptyFolders = null,
                DuplicateSd = duplicateSd,
                ConfirmedDuplicate = 0,
                Inserted = inserted,
                NotApplicableOthers = others,
                Total = migratedImages + duplicateSd + inserted + others,
            });
        }

        return rows;
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
