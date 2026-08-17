using DCT_SD.Configuration;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.ManualValidation;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class ManualValidationService : IManualValidationService
{
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
        var query = _context.ManualValidationRemarks.AsNoTracking().Where(r => r.ManualValidationRequestId == id);

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

    private static ManualValidationRemarkDto MapToRemarkDto(ManualValidationRemark r) => new()
    {
        Id = r.Id,
        UpdatedAt = r.CreatedAt,
        By = r.ByUsername,
        Remarks = r.Remarks,
        Action = r.Action.ToString(),
    };

    public async Task<ManualValidationDetailDto> OpenForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, includeDocuments: true, cancellationToken);

        record.LockedByUserId = _currentUserService.UserId;
        record.LockedByUsername = _currentUserService.Username;
        record.LockedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(record);
    }

    public async Task<ManualValidationDetailDto> SaveAsync(int id, SaveManualValidationRequestDto request, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, includeDocuments: true, cancellationToken);

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
            : await _context.RegistryOffices.AsNoTracking()
                .Where(o => o.Code == record.RdCode)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(cancellationToken);

        record.MissingFieldsCsv = string.Join(',', ComputeMissingFields(record));
        record.UpdatedByUserId = _currentUserService.UserId;
        record.UpdatedByUsername = _currentUserService.Username;
        record.UpdatedAt = DateTime.UtcNow;

        _context.ManualValidationRemarks.Add(new ManualValidationRemark
        {
            ManualValidationRequestId = record.Id,
            Action = RemarkAction.Saved,
            Remarks = "Record details updated during manual validation.",
            ByUserId = _currentUserService.UserId ?? 0,
            ByUsername = _currentUserService.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDetail(record);
    }

    public async Task CloseAsync(int id, string remarks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            throw new BusinessValidationException("Remarks is required.");
        }

        var record = await GetActiveRecordAsync(id, includeDocuments: false, cancellationToken);

        _context.ManualValidationRemarks.Add(new ManualValidationRemark
        {
            ManualValidationRequestId = record.Id,
            Action = RemarkAction.Closed,
            Remarks = remarks.Trim(),
            ByUserId = _currentUserService.UserId ?? 0,
            ByUsername = _currentUserService.Username ?? "system",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MigrateAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await GetActiveRecordAsync(id, includeDocuments: false, cancellationToken);

        if (ComputeMissingFields(record).Length > 0)
        {
            throw new BusinessValidationException("Please complete all mandatory fields before proceeding with migration.");
        }

        record.MigratedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TitleSequenceDto> RetrieveTitleSequenceAsync(RetrieveTitleSequenceRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TitleType>(request.TitleType, true, out var titleType))
        {
            throw new NotFoundException("No matching title sequence found for the title record.");
        }

        var lookup = await _context.TitleSequenceLookups.AsNoTracking()
            .FirstOrDefaultAsync(t =>
                t.Title == request.Title.Trim() &&
                t.TitleType == titleType &&
                t.Plan == request.Plan.Trim() &&
                t.Block == request.Block.Trim() &&
                t.Lot == request.Lot.Trim(),
                cancellationToken)
            ?? throw new NotFoundException("No matching title sequence found for the title record.");

        return new TitleSequenceDto { Sequence = lookup.Sequence };
    }

    private async Task<ManualValidationRequest> GetActiveRecordAsync(int id, bool includeDocuments, CancellationToken cancellationToken)
    {
        var query = _context.ManualValidationRequests.Where(r => r.Id == id && r.MigratedAt == null);
        if (includeDocuments)
        {
            query = query.Include(r => r.Documents);
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Manual validation record", id);
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
        MissingFields = r.MissingFieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        Documents = r.Documents.Select(d => new ManualValidationDocumentDto
        {
            Id = d.Id,
            DocumentName = d.DocumentName,
            FileName = d.FileName,
        }).ToArray(),
    };
}
