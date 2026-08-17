using DCT_SD.Configuration;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Migrations;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class MigrationService : IMigrationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MigrationService(ApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<MigrationListItemDto>> SearchAsync(MigrationSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.MigrationRecords.AsNoTracking().AsQueryable();

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

        if (!string.IsNullOrWhiteSpace(request.MigrationStatus) && Enum.TryParse<MigrationStatus>(request.MigrationStatus, true, out var status))
        {
            query = query.Where(r => r.MigrationStatus == status);
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.MigrationDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.MigrationDate <= request.DateTo.Value);
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.MigrationDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<MigrationListItemDto>
        {
            Items = items.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    private static MigrationListItemDto MapToListItem(MigrationRecord r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        MigrationDate = r.MigrationDate,
        RdCode = r.RdCode,
        RdName = r.RdName,
        EntryNumbersCsv = r.EntryNumbersCsv,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        MigrationStatus = r.MigrationStatus.ToString(),
        SdStatus = r.SdStatus.ToString(),
        MigratedTo = r.MigratedToRdName,
    };

    public async Task<MigrationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var record = await _context.MigrationRecords.AsNoTracking()
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Migration record", id);

        return MapToDetail(record);
    }

    public async Task<MigrationDetailDto> OverwriteDocumentAsync(int migrationId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetDuplicateDocumentForActionAsync(migrationId, documentId, cancellationToken);

        document.Status = MigrationDocumentStatus.Overwritten;
        document.PerformedByUserId = _currentUserService.UserId;
        document.PerformedByUsername = _currentUserService.Username;
        document.ActionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await ResolveSdStatusIfFullyMigratedAsync(migrationId, cancellationToken);

        return await GetByIdAsync(migrationId, cancellationToken);
    }

    public async Task<MigrationDetailDto> InsertAsNewDocumentAsync(int migrationId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await GetDuplicateDocumentForActionAsync(migrationId, documentId, cancellationToken);

        document.Status = MigrationDocumentStatus.InsertedAsNew;
        document.PerformedByUserId = _currentUserService.UserId;
        document.PerformedByUsername = _currentUserService.Username;
        document.ActionDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        await ResolveSdStatusIfFullyMigratedAsync(migrationId, cancellationToken);

        return await GetByIdAsync(migrationId, cancellationToken);
    }

    // Once every previously-duplicate supporting document under this migration record has been
    // resolved via Overwrite/Insert-as-New, no DuplicateSd rows remain - at that point the record's
    // SD Status graduates to AllMigrated (existing enum/display value, previously only ever set at seed time).
    private async Task ResolveSdStatusIfFullyMigratedAsync(int migrationId, CancellationToken cancellationToken)
    {
        var hasRemainingDuplicates = await _context.MigrationDocuments
            .AnyAsync(d => d.MigrationRecordId == migrationId && d.Status == MigrationDocumentStatus.DuplicateSd, cancellationToken);

        if (hasRemainingDuplicates)
        {
            return;
        }

        var record = await _context.MigrationRecords.FirstAsync(r => r.Id == migrationId, cancellationToken);
        if (record.SdStatus != SupportingDocumentStatus.AllMigrated)
        {
            record.SdStatus = SupportingDocumentStatus.AllMigrated;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<MigrationDocument> GetDuplicateDocumentForActionAsync(int migrationId, int documentId, CancellationToken cancellationToken)
    {
        var document = await _context.MigrationDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.MigrationRecordId == migrationId, cancellationToken)
            ?? throw new NotFoundException("Supporting document", documentId);

        if (document.Status != MigrationDocumentStatus.DuplicateSd)
        {
            throw new ConflictException("This supporting document has already been resolved.");
        }

        return document;
    }

    private static MigrationDetailDto MapToDetail(MigrationRecord r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        MigrationDate = r.MigrationDate,
        RdCode = r.RdCode,
        RdName = r.RdName,
        EntryNumbersCsv = r.EntryNumbersCsv,
        Title = r.Title,
        TitleType = r.TitleType?.ToString(),
        Plan = r.Plan,
        Block = r.Block,
        Lot = r.Lot,
        TitleSequence = r.TitleSequence,
        MigrationStatus = r.MigrationStatus.ToString(),
        SdStatus = r.SdStatus.ToString(),
        MigratedTo = r.MigratedToRdName,
        Documents = r.Documents
            .OrderBy(d => d.DocumentName)
            .ThenBy(d => d.FileName)
            .Select(d => new MigrationDocumentDto
            {
                Id = d.Id,
                DocumentName = d.DocumentName,
                FileName = d.FileName,
                Status = d.Status.ToString(),
                ExistingFileName = d.ExistingFileName,
                PerformedBy = d.PerformedByUsername,
                ActionDate = d.ActionDate,
            })
            .ToArray(),
    };
}
