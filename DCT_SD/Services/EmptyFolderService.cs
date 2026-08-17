using DCT_SD.Configuration;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.EmptyFolders;
using DCT_SD.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class EmptyFolderService : IEmptyFolderService
{
    private readonly ApplicationDbContext _context;

    public EmptyFolderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<EmptyFolderListItemDto>> SearchAsync(EmptyFolderSearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.EmptyFolderRecords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.RdCode))
        {
            query = query.Where(r => r.RdCode == request.RdCode);
        }

        if (!string.IsNullOrWhiteSpace(request.FolderName))
        {
            var term = request.FolderName.Trim().ToLower();
            query = query.Where(r => r.FolderName.ToLower().Contains(term));
        }

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.FetchDateTime >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.FetchDateTime <= request.DateTo.Value);
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.FetchDateTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmptyFolderListItemDto>
        {
            Items = items.Select(MapToListItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    private static EmptyFolderListItemDto MapToListItem(EmptyFolderRecord r) => new()
    {
        Id = r.Id,
        FetchDateTime = r.FetchDateTime,
        RdCode = r.RdCode,
        RdName = r.RdName,
        FolderName = r.FolderName,
        FolderPath = r.FolderPath,
        Status = r.Status,
    };
}
