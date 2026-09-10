using System.Text.Json;
using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class ManualValidationService : IManualValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // How long a record stays locked to the user who opened it before it's treated as
    // abandoned (browser closed, network drop, etc.) and released automatically. Without this,
    // a lock nobody ever explicitly releases would block that record forever for everyone else.
    private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(15);

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ManualValidationService(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<ManualValidationListItemDto>> SearchAsync(ManualValidationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.ManualValidationRequests.AsNoTracking().Where(r => r.MigratedAt == null);

        if (!string.IsNullOrWhiteSpace(request.RdCode))
        {
            query = query.Where(r => r.RdCode == request.RdCode);
        }

        if (!string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            var term = request.RequestNumber.Trim().ToLower();
            query = query.Where(r => r.RequestNumber.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.EntryNumbersCsv))
        {
            var term = request.EntryNumbersCsv.Trim().ToLower();
            query = query.Where(r => r.EntryNumbersCsv != null && r.EntryNumbersCsv.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var term = request.Title.Trim().ToLower();
            query = query.Where(r => r.Title != null && r.Title.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ManualValidationStatus>(request.Status, true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.ExtractionDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.ExtractionDate <= request.DateTo.Value);
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.ExtractionDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ManualValidationListItemDto>
        {
            Items = items.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<PagedResult<ManualValidationRemarkDto>> GetRemarksHistoryAsync(int id, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.RecordHistory.AsNoTracking()
            .Where(r => r.TableName == RecordHistoryTables.ManualValidationRequests && r.RecordId == id);

        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize is < 1 or > 100 ? 25 : pageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ManualValidationRemarkDto>
        {
            Items = items.Select(MapToRemarkDto).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    private static ManualValidationListItemDto MapToListItem(ManualValidationRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        RdCode = r.RdCode,
        RdName = r.RdName,
        EntryNumbersCsv = r.EntryNumbersCsv,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        Status = r.Status.ToString(),
        MissingFields = r.MissingFieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ExtractionDate = r.ExtractionDate,
        UpdatedBy = r.UpdatedByUsername,
        UpdatedDate = r.UpdatedAt,
    };

    private static ManualValidationRemarkDto MapToRemarkDto(RecordHistory r) => new()
    {
        Id = r.Id,
        UpdatedAt = r.CreatedAt,
        By = r.ByUsername ?? string.Empty,
        Remarks = r.Remarks ?? string.Empty,
        Action = r.Action,
    };

    public async Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        record.LockedByUserId = _currentUserService.UserId;
        record.LockedByUsername = _currentUserService.Username;
        record.LockedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(record);
    }

    public async Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        record.RdCode = request.RdCode?.Trim();
        record.EntryNumbersCsv = request.EntryNumbersCsv?.Trim();
        record.Title = request.Title?.Trim();
        record.TitleType = Enum.TryParse<TitleType>(request.TitleType, true, out var titleType) ? titleType : null;
        record.Plan = request.Plan?.Trim();
        record.Block = request.Block?.Trim();
        record.Lot = request.Lot?.Trim();
        record.TitleSequence = request.TitleSequence?.Trim();

        record.RdName = string.IsNullOrWhiteSpace(record.RdCode)
            ? null
            : await _context.CodeLookups.AsNoTracking()
                .Where(c => c.LookupType == CodeLookupTypes.RegistryOffice && c.Code == record.RdCode)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);

        record.MissingFieldsCsv = string.Join(',', ComputeMissingFields(record));
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = _currentUserService.Username;
        record.UpdatedAt = DateTime.UtcNow;

        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.ManualValidationRequests,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = RemarkAction.Saved.ToString(),
            Remarks = "Record details updated during manual validation.",
            ByUserId = _currentUserService.UserId,
            ByUsername = _currentUserService.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        // Others -> real Document Type correction: the physical file is renamed (never the
        // containing folder) and DocumentsJson updated only here, as part of Save itself - never
        // when the dropdown selection changes. If the rename fails, nothing below has touched the
        // database yet, so there's nothing to roll back; if the rename succeeds but the database
        // save then fails for some other reason, the rename is undone so the file, DocumentsJson,
        // and imagePath all stay consistent with each other either way.
        (string NewPath, string OriginalPath)? renameToRollBack = null;
        if (request.DocumentChangeIndex is { } changeIndex && changeIndex > 0
            && !string.IsNullOrWhiteSpace(request.DocumentChangeCode) && !string.IsNullOrWhiteSpace(request.DocumentChangeName))
        {
            renameToRollBack = ApplyDocumentTypeChange(record, changeIndex, request.DocumentChangeCode.Trim(), request.DocumentChangeName.Trim());
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (renameToRollBack is { } rollback && System.IO.File.Exists(rollback.NewPath))
            {
                System.IO.File.Move(rollback.NewPath, rollback.OriginalPath);
            }

            throw;
        }

        return MapToDetail(record);
    }

    // Renames the physical image file to "<DocumentID>_<DocumentName><extension>" (falling back
    // to a numeric suffix, the same disambiguation convention the OCR pipeline's own
    // renamedFileName already uses, only if that exact name is already taken by a different
    // file) and updates the matching item's documentId/documentName/renamedFileName/imagePath in
    // DocumentsJson. Returns the (new, original) path pair so the caller can undo the rename if
    // the database save that follows ends up failing.
    private static (string NewPath, string OriginalPath)? ApplyDocumentTypeChange(ManualValidationRequest record, int changeIndex, string newCode, string newName)
    {
        var items = string.IsNullOrWhiteSpace(record.DocumentsJson)
            ? []
            : JsonSerializer.Deserialize<List<DocumentJsonItem>>(record.DocumentsJson, JsonOptions) ?? [];

        var sorted = items
            .OrderBy(d => d.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.RenamedFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var target = changeIndex - 1 < sorted.Count ? sorted[changeIndex - 1] : null;
        if (target is null || string.IsNullOrWhiteSpace(target.ImagePath))
        {
            return null;
        }

        if (!System.IO.File.Exists(target.ImagePath))
        {
            throw new BusinessValidationException("The original image file could not be found on disk. The Document Type change was not saved.");
        }

        var directory = Path.GetDirectoryName(target.ImagePath)!;
        var extension = Path.GetExtension(target.ImagePath);
        var baseName = $"{newCode}_{newName}";

        var newFileName = baseName + extension;
        var newPath = Path.Combine(directory, newFileName);
        var suffix = 1;
        while (System.IO.File.Exists(newPath) && !string.Equals(newPath, target.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            suffix++;
            newFileName = $"{baseName}_{suffix}{extension}";
            newPath = Path.Combine(directory, newFileName);
        }

        (string NewPath, string OriginalPath)? rollback = null;
        if (!string.Equals(newPath, target.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            System.IO.File.Move(target.ImagePath, newPath);
            rollback = (newPath, target.ImagePath);
        }

        target.DocumentId = newCode;
        target.DocumentName = newName;
        target.RenamedFileName = newFileName;
        target.ImagePath = newPath;

        record.DocumentsJson = JsonSerializer.Serialize(items, JsonOptions);
        return rollback;
    }

    public async Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessValidationException("Remarks is required.");
        }

        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        // Closing is itself a save on the record (it's the action that finalizes it), so it
        // must update Updated By/Date the same way SaveAsync does - otherwise a record closed
        // without ever going through the separate Save button keeps showing whatever (possibly
        // stale, possibly never-set) values it had before.
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = _currentUserService.Username;
        record.UpdatedAt = DateTime.UtcNow;

        _context.RecordHistory.Add(new RecordHistory
        {
            TableName = RecordHistoryTables.ManualValidationRequests,
            RecordId = record.Id,
            RefNo = record.RequestNumber,
            Action = RemarkAction.Closed.ToString(),
            Remarks = remarks.Trim(),
            ByUserId = _currentUserService.UserId,
            ByUsername = _currentUserService.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MigrateAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, cancellationToken);
        EnsureNotLockedByAnotherUser(record);

        if (ComputeMissingFields(record).Length > 0)
        {
            throw new BusinessValidationException("Please complete all mandatory fields before proceeding with migration.");
        }

        record.MigratedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Matching is staged per the acceptance criteria:
    //   1. RD Code + Title Number + Title Type.
    //   2. If still ambiguous, narrow further using Plan/Block/Lot (TCT/OCT - the only two
    //      TitleType values this codebase has, see Models/Enums/TitleType.cs).
    //   3. If still ambiguous after that (a genuinely Repeating Title Number), return every
    //      remaining candidate for the caller to show a manual-selection window instead of
    //      picking one arbitrarily.
    // RD Code isn't part of CodeLookups.Code's existing composite key/DataJson for any row in
    // the live data today, so it's only enforced against rows whose DataJson actually records
    // one - rows without it are never excluded on that basis, which keeps every existing
    // Title Sequence lookup working exactly as it does today.
    public async Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TitleType>(request.TitleType, true, out var titleType))
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        var rdCode = request.RdCode.Trim();
        var title = request.Title.Trim();
        var plan = request.Plan.Trim();
        var block = request.Block.Trim();
        var lot = request.Lot.Trim();

        var rows = await _context.CodeLookups.AsNoTracking()
            .Where(c => c.LookupType == CodeLookupTypes.TitleSequence && c.IsActive)
            .ToListAsync(cancellationToken);

        var parsed = rows
            .Select(r => (Row: r, Data: DeserializeTitleSequenceData(r.DataJson)))
            .Where(x => x.Data is not null)
            .ToList();

        var stage1 = parsed.Where(x =>
            string.Equals(x.Data!.Title?.Trim(), title, StringComparison.OrdinalIgnoreCase)
            && x.Data.TitleType == (int)titleType
            && (string.IsNullOrWhiteSpace(x.Data.RdCode) || string.Equals(x.Data.RdCode!.Trim(), rdCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (stage1.Count == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        if (stage1.Count == 1)
        {
            return new TitleSequenceDto { Sequence = stage1[0].Row.Name };
        }

        var stage2 = stage1.Where(x =>
            string.Equals(x.Data!.Plan?.Trim(), plan, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Data.Block?.Trim(), block, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Data.Lot?.Trim(), lot, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (stage2.Count == 0)
        {
            throw new NotFoundException("No Title Sequence record was found.");
        }

        if (stage2.Count == 1)
        {
            return new TitleSequenceDto { Sequence = stage2[0].Row.Name };
        }

        return new TitleSequenceDto
        {
            IsAmbiguous = true,
            Candidates = stage2.Select(x => new TitleSequenceCandidateDto
            {
                RdCode = x.Data!.RdCode,
                Title = x.Data.Title ?? title,
                TitleType = titleType.ToString(),
                Plan = x.Data.Plan,
                Block = x.Data.Block,
                Lot = x.Data.Lot,
                Sequence = x.Row.Name,
            }).ToArray(),
        };
    }

    private static TitleSequenceDataJson? DeserializeTitleSequenceData(string? dataJson)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TitleSequenceDataJson>(dataJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private class TitleSequenceDataJson
    {
        public string? RdCode { get; set; }
        public string? Title { get; set; }
        public int? TitleType { get; set; }
        public string? Plan { get; set; }
        public string? Block { get; set; }
        public string? Lot { get; set; }
    }

    private async Task<ManualValidationRequest> GetActiveRecordAsync(int id, CancellationToken cancellationToken) =>
        await _context.ManualValidationRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.MigratedAt == null, cancellationToken)
            ?? throw new NotFoundException("Manual validation record", id);

    private void EnsureNotLockedByAnotherUser(ManualValidationRequest record)
    {
        var lockedByAnotherUser = record.LockedByUserId.HasValue
            && record.LockedByUserId != _currentUserService.UserId
            && record.LockedAt.HasValue
            && DateTime.UtcNow - record.LockedAt.Value < LockTimeout;

        if (lockedByAnotherUser)
        {
            throw new ForbiddenAppException("This record is currently being reviewed by another user.");
        }
    }

    private static string[] ComputeMissingFields(ManualValidationRequest r)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(r.RdCode)) missing.Add("rdCode");
        if (string.IsNullOrWhiteSpace(r.RdName)) missing.Add("rdName");
        if (string.IsNullOrWhiteSpace(r.EntryNumbersCsv)) missing.Add("entry");
        if (string.IsNullOrWhiteSpace(r.Title)) missing.Add("title");
        if (r.TitleType is null) missing.Add("titleType");
        if (string.IsNullOrWhiteSpace(r.Plan)) missing.Add("plan");
        if (string.IsNullOrWhiteSpace(r.Block)) missing.Add("block");
        if (string.IsNullOrWhiteSpace(r.Lot)) missing.Add("lot");
        if (string.IsNullOrWhiteSpace(r.TitleSequence)) missing.Add("titleSequence");
        return missing.ToArray();
    }

    private static ManualValidationDetailDto MapToDetail(ManualValidationRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        RdCode = r.RdCode,
        RdName = r.RdName,
        EntryNumbersCsv = r.EntryNumbersCsv,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        Plan = r.Plan,
        Block = r.Block,
        Lot = r.Lot,
        TitleSequence = r.TitleSequence,
        Status = r.Status.ToString(),
        // Computed live from the record's actual field values (same rule the Save path already
        // uses) rather than trusting the stored MissingFieldsCsv - that column is populated by
        // the external OCR pipeline at record-creation time and doesn't reliably reflect which
        // Title Record fields are actually blank (e.g. leaves plan/block unflagged even when
        // empty, and carries unrelated keys like "documentClassification").
        MissingFields = ComputeMissingFields(r),
        Documents = ParseDocuments(r.DocumentsJson),
    };

    // ManualValidationRequests.DocumentsJson holds a JSON array of {documentId, documentTypeCode,
    // documentName, originalFileName, renamedFileName, imagePath} - no per-item id in storage,
    // so one is synthesized from (sorted) position for the API/UI. Sorted by Document Name then
    // Image File Name (renamedFileName) so the Supporting Documents list and the image viewer's
    // Prev/Next order match. This is the single place DocumentsJson gets parsed and sorted -
    // ParseDocuments (the client-facing list) and GetDocumentImagePathAsync (the image lookup)
    // both build on it, so a document's position/Id means the same thing in both.
    private static List<DocumentJsonItem> ParseAndSortDocumentItems(string? documentsJson)
    {
        if (string.IsNullOrWhiteSpace(documentsJson))
        {
            return [];
        }

        var items = JsonSerializer.Deserialize<List<DocumentJsonItem>>(documentsJson, JsonOptions) ?? [];
        return items
            .OrderBy(d => d.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.RenamedFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ManualValidationDocumentDto[] ParseDocuments(string? documentsJson) =>
        ParseAndSortDocumentItems(documentsJson)
            .Select((d, index) => new ManualValidationDocumentDto
            {
                Id = index + 1,
                DocumentId = d.DocumentId,
                DocumentName = d.DocumentName,
                RenamedFileName = d.RenamedFileName,
            }).ToArray();

    public async Task<string?> GetDocumentImagePathAsync(int id, int documentId, CancellationToken cancellationToken = default)
    {
        var documentsJson = await _context.ManualValidationRequests.AsNoTracking()
            .Where(r => r.Id == id && r.MigratedAt == null)
            .Select(r => r.DocumentsJson)
            .FirstOrDefaultAsync(cancellationToken);

        var items = ParseAndSortDocumentItems(documentsJson);
        var index = documentId - 1;
        return index >= 0 && index < items.Count ? items[index].ImagePath : null;
    }

    private class DocumentJsonItem
    {
        public string? DocumentId { get; set; }
        public string? DocumentTypeCode { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string? OriginalFileName { get; set; }
        public string RenamedFileName { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }
}
