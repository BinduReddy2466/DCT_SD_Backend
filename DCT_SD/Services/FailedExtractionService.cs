using DCT_SD.Configuration;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.FailedExtraction;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

// Failed Extraction has no dedicated table of its own - it reads OcrExtractionRecords rows
// where ExtractionStatus == Failed, with the failure reason attached via the existing generic
// RecordHistory table (TableName="OcrExtractionRecords"), the same way Manual Validation
// remarks are stored. No schema change; OcrExtractionRecord has no FolderName column either,
// so it's derived from FolderPath.
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
        var query = _context.OcrExtractionRecords.AsNoTracking()
            .Where(r => r.ExtractionStatus == OcrExtractionStatus.Failed);

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
            query = query.Where(r => r.ExtractionDateTime <= request.DateTo.Value);
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var records = await query
            .OrderByDescending(r => r.ExtractionDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var reasonsById = await GetLatestFailureReasonsAsync(records.Select(r => r.Id), cancellationToken);

        return new PagedResult<FailedExtractionListItemDto>
        {
            Items = records.Select(r => MapToListItem(r, reasonsById.GetValueOrDefault(r.Id, string.Empty))).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public Task<bool> AnyRecordsExistAsync(CancellationToken cancellationToken = default) =>
        _context.OcrExtractionRecords.AsNoTracking().AnyAsync(r => r.ExtractionStatus == OcrExtractionStatus.Failed, cancellationToken);

    public async Task RecordFailureAsync(string requestNumber, string? rdCode, string? rdName, string folderPath, string failureReason, DateTime extractionDateTime, CancellationToken cancellationToken = default)
    {
        var record = new OcrExtractionRecord
        {
            RequestNumber = requestNumber,
            RdCode = rdCode,
            RdName = rdName,
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

    private static string ExtractFolderName(string folderPath)
    {
        var trimmed = folderPath.TrimEnd('\\', '/');
        var separatorIndex = trimmed.LastIndexOfAny(['\\', '/']);
        return separatorIndex >= 0 ? trimmed[(separatorIndex + 1)..] : trimmed;
    }
}
