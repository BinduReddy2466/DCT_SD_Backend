using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.FailedExtraction;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

// The Failed Extraction LIST is read directly from the existing FailedExtractionRecords table
// (populated externally by the OCR/fetch pipeline, mirroring OcrExtractionRecords/
// EmptyFolderRecords - see FailedExtractionRecord.cs); each row's own Id is what the Reprocess
// button now sends. Everything AFTER that first lookup (GetFolderPathByIdAsync) is unchanged
// Reprocess business logic, still keyed on OcrExtractionRecords + RecordHistory exactly as
// before (GetActiveFailedRecordByFolderPathAsync, UpdateFailureAsync, RemoveFailedRecordAsync,
// HasSuccessfulRecordForFolderAsync, RecordFailureAsync) - resolving the FolderPath from the
// actual displayed FailedExtractionRecords row up front just makes sure the folder the external
// service is asked to retry is exactly the one shown on screen.
public class FailedExtractionService : IFailedExtractionService
{
    private const string RecordHistoryTableName = "OcrExtractionRecords";

    private readonly ApplicationDbContext _context;

    public FailedExtractionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<FailedExtractionListItemDto>> SearchAsync(FailedExtractionSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.FailedExtractionRecords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Rd))
        {
            var term = request.Rd.Trim().ToLower();
            query = query.Where(r => r.RdName != null && r.RdName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.FolderName))
        {
            var term = request.FolderName.Trim().ToLower();
            query = query.Where(r => r.FolderPath.ToLower().Contains(term));
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.ExtractionDateTime >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.ExtractionDateTime < DateRangeFilter.EndOfDayExclusive(request.DateTo.Value));
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(r => r.ExtractionDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FailedExtractionListItemDto>
        {
            Items = records.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public Task<bool> AnyRecordsExistAsync(CancellationToken cancellationToken = default) =>
        _context.FailedExtractionRecords.AsNoTracking().AnyAsync(cancellationToken);

    public Task<string?> GetFolderPathByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.FailedExtractionRecords.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => (string?)r.FolderPath)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task RecordFailureAsync(string requestNumber, string? rdCode, string? rdName, string folderPath, string failureReason, DateTime extractionDateTime, int? fetchRunId = null, CancellationToken cancellationToken = default)
    {
        // The live fetch stream's folder_result event only ever carries rd_code, never rd_name -
        // resolve it the same way Manual Validation already does, via the existing CodeLookups
        // table, so the Failed Extraction page's "RD" column isn't left blank for every real
        // failure.
        var resolvedRdName = rdName;
        if (string.IsNullOrWhiteSpace(resolvedRdName) && !string.IsNullOrWhiteSpace(rdCode))
        {
            resolvedRdName = await _context.CodeLookups.AsNoTracking()
                .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.Code == rdCode)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var record = new OcrExtractionRecord
        {
            RequestNumber = requestNumber,
            FetchRunId = fetchRunId,
            RdCode = rdCode,
            RdName = resolvedRdName,
            FolderPath = folderPath,
            DocumentCount = 0,
            ExtractionStatus = OcrExtractionStatus.Failed,
            ExtractionDateTime = extractionDateTime,
        };

        _context.OcrExtractionRecords.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTableName,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = "ExtractionFailed",
            Remarks = failureReason,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<FailedExtractionListItemDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.OcrExtractionRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.ExtractionStatus == OcrExtractionStatus.Failed, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var reasonsById = await GetLatestFailureReasonsAsync([record.Id], cancellationToken);
        return MapToListItem(record, reasonsById.GetValueOrDefault(record.Id, string.Empty));
    }

    public async Task<FailedExtractionListItemDto?> GetActiveFailedRecordByFolderPathAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        var record = await _context.OcrExtractionRecords.AsNoTracking()
            .Where(r => r.ExtractionStatus == OcrExtractionStatus.Failed && r.FolderPath == folderPath)
            .OrderByDescending(r => r.ExtractionDateTime)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return null;
        }

        var reasonsById = await GetLatestFailureReasonsAsync([record.Id], cancellationToken);
        return MapToListItem(record, reasonsById.GetValueOrDefault(record.Id, string.Empty));
    }

    public async Task UpdateFailureAsync(int id, string failureReason, DateTime extractionDateTime, CancellationToken cancellationToken = default)
    {
        var record = await _context.OcrExtractionRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.ExtractionStatus == OcrExtractionStatus.Failed, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.ExtractionDateTime = extractionDateTime;
        await _context.SaveChangesAsync(cancellationToken);

        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTableName,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = "ExtractionFailed",
            Remarks = failureReason,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Updates the FailureReason column on the FailedExtractionRecords row itself - the row the
    // list page/grid is actually built from (see SearchAsync/MapToListItem) - as opposed to
    // UpdateFailureAsync, which only touches the separate OcrExtractionRecords+RecordHistory
    // bookkeeping the Reprocess action's own success/failure logic uses internally. Both are kept
    // in sync from the Reprocess action so the grid reflects the latest attempt's outcome.
    public async Task UpdateFailedExtractionRecordReasonAsync(int id, string? failureReason, CancellationToken cancellationToken = default)
    {
        var record = await _context.FailedExtractionRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.FailureReason = failureReason ?? string.Empty;
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Removes a resolved Failed Extraction record: the external reprocess service confirms
    // success in its own response (records_created/manual_validation_created > 0) and clears the
    // folder from its own internal failure tracking, but - confirmed by directly querying that
    // service's own GET /failed-extractions right after a real success - it does not go back and
    // update/remove this app's already-written OcrExtractionRecords row for the same folder, so
    // this app removes it itself once the response proves the retry actually succeeded. Deletes
    // the row (not just its status) since a real, separate OcrExtractionRecords row was already
    // created for the successful attempt - keeping the old Failed one around would be a stale
    // duplicate of the same folder, one Failed and one not.
    public async Task RemoveFailedRecordAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.OcrExtractionRecords
            .FirstOrDefaultAsync(r => r.Id == id && r.ExtractionStatus == OcrExtractionStatus.Failed, cancellationToken);
        if (record is null)
        {
            return;
        }

        var historyEntries = await _context.RecordHistory
            .Where(h => h.TableName == RecordHistoryTableName && h.RecordId == id)
            .ToListAsync(cancellationToken);
        _context.RecordHistory.RemoveRange(historyEntries);
        _context.OcrExtractionRecords.Remove(record);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasSuccessfulRecordForFolderAsync(string folderPath, CancellationToken cancellationToken = default) =>
        _context.OcrExtractionRecords.AsNoTracking()
            .AnyAsync(r => r.FolderPath == folderPath && r.ExtractionStatus != OcrExtractionStatus.Failed, cancellationToken);

    private async Task<Dictionary<int, string>> GetLatestFailureReasonsAsync(IEnumerable<int> recordIds, CancellationToken cancellationToken)
    {
        var ids = recordIds.ToArray();
        if (ids.Length == 0) return new Dictionary<int, string>();

        var entries = await _context.RecordHistory.AsNoTracking()
            .Where(h => h.TableName == RecordHistoryTableName && h.RecordId != null && ids.Contains(h.RecordId.Value))
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(h => h.RecordId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Remarks ?? string.Empty);
    }

    // Used only by GetByIdAsync/GetActiveFailedRecordByFolderPathAsync - the Reprocess-support
    // reads that still work off OcrExtractionRecords, unchanged.
    private static FailedExtractionListItemDto MapToListItem(OcrExtractionRecord r, string failureReason) => new()
    {
        Id = r.Id,
        ExtractionDateTime = r.ExtractionDateTime,
        RdCode = r.RdCode,
        RdName = r.RdName,
        FolderName = ExtractFolderName(r.FolderPath),
        FolderPath = r.FolderPath,
        FailureReason = failureReason,
    };

    // Used by SearchAsync - the list display, read directly from FailedExtractionRecords. Id is
    // this row's own FailedExtractionRecords.Id, which the Reprocess button now sends;
    // GetFolderPathByIdAsync resolves it back to this same row's FolderPath when clicked.
    private static FailedExtractionListItemDto MapToListItem(FailedExtractionRecord r) => new()
    {
        Id = r.Id,
        ExtractionDateTime = r.ExtractionDateTime,
        RdCode = r.RdCode,
        RdName = r.RdName,
        FolderName = r.FolderName,
        FolderPath = r.FolderPath,
        FailureReason = r.FailureReason,
    };

    private static string ExtractFolderName(string folderPath)
    {
        var trimmed = folderPath.TrimEnd('\\', '/');
        var separatorIndex = trimmed.LastIndexOfAny(['\\', '/']);
        return separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : trimmed;
    }
}
