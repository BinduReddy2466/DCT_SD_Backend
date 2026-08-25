using DCT_SD.Configuration;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.RdConfig;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class RdConfigService : IRdConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RdConfigService(ApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RootPathDto> GetCurrentRootPathAsync(CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestHistoryAsync(cancellationToken);
        return new RootPathDto { CurrentPath = latest?.SourcePath };
    }

    public async Task<RootPathDto> UpdateRootPathAsync(UpdateRootPathRequestDto request, CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestHistoryAsync(cancellationToken);
        var newPath = request.NewPath.Trim();

        if (latest is not null && string.Equals(latest.SourcePath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessValidationException("The selected Root Source Path is the same as the current configuration. No changes have been made.");
        }

        var history = new FetchRun
        {
            RecordKind = FetchRunRecordKinds.PathChange,
            FromPath = latest?.SourcePath,
            SourcePath = newPath,
            Remarks = request.Remarks.Trim(),
            ExecutedByUserId = _currentUser.UserId ?? 0,
            ExecutedByUsername = _currentUser.Username ?? "unknown",
            StartedAt = DateTime.UtcNow,
        };

        _context.FetchRuns.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        return new RootPathDto { CurrentPath = history.SourcePath };
    }

    public async Task<PagedResult<RootPathHistoryItemDto>> SearchRootPathHistoryAsync(RootPathHistorySearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.FetchRuns.AsNoTracking().Where(h => h.RecordKind == FetchRunRecordKinds.PathChange);

        if (request.DateFrom.HasValue)
        {
            query = query.Where(h => h.StartedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(h => h.StartedAt <= request.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ModifiedBy))
        {
            var term = request.ModifiedBy.Trim().ToLower();
            query = query.Where(h => h.ExecutedByUsername.ToLower().Contains(term));
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(h => h.StartedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RootPathHistoryItemDto>
        {
            Items = items.Select(MapToHistoryItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public async Task<FetchRunItemDto> StartFetchAsync(CancellationToken cancellationToken = default)
    {
        var latest = await GetLatestHistoryAsync(cancellationToken);
        if (latest is null)
        {
            throw new BusinessValidationException("The root source path must be configured before starting a fetch.");
        }

        var hasOngoing = await _context.FetchRuns.AnyAsync(r => r.Status == FetchRunStatus.Ongoing, cancellationToken);
        if (hasOngoing)
        {
            throw new BusinessValidationException("A fetch is already in progress. Please wait for it to complete.");
        }

        var run = new FetchRun
        {
            RecordKind = FetchRunRecordKinds.FetchRun,
            SourcePath = latest.SourcePath,
            Status = FetchRunStatus.Ongoing,
            ProcessedCount = 0,
            TotalCount = null,
            ExecutedByUserId = _currentUser.UserId ?? 0,
            ExecutedByUsername = _currentUser.Username ?? "unknown",
            StartedAt = DateTime.UtcNow,
        };

        _context.FetchRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToFetchRunItem(run);
    }

    public async Task<PagedResult<FetchRunItemDto>> SearchFetchHistoryAsync(FetchHistorySearchRequestDto request, CancellationToken cancellationToken = default)
    {
        var query = _context.FetchRuns.AsNoTracking().Where(r => r.RecordKind == FetchRunRecordKinds.FetchRun);

        if (request.DateFrom.HasValue)
        {
            query = query.Where(r => r.StartedAt >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(r => r.StartedAt <= request.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ExecutedBy))
        {
            var term = request.ExecutedBy.Trim().ToLower();
            query = query.Where(r => r.ExecutedByUsername.ToLower().Contains(term));
        }

        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize is < 1 or > 100 ? 25 : request.PageSize;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FetchRunItemDto>
        {
            Items = items.Select(MapToFetchRunItem).ToArray(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };
    }

    public Task<DirectoryBrowseDto> BrowseDirectoriesAsync(string? path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var drives = GetAllowedDrives()
                .Select(d => new DirectoryEntryDto { Name = d.Name.TrimEnd('\\'), FullPath = d.Name })
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult(new DirectoryBrowseDto { CurrentPath = null, ParentPath = null, Directories = drives });
        }

        string normalized;
        try
        {
            normalized = ValidateAndNormalizePath(path);
        }
        catch (BusinessValidationException ex)
        {
            return Task.FromResult(new DirectoryBrowseDto { CurrentPath = null, ParentPath = null, Error = ex.Message });
        }

        List<DirectoryEntryDto> directories;
        try
        {
            directories = Directory.GetDirectories(normalized)
                .Select(d => new DirectoryEntryDto { Name = Path.GetFileName(d), FullPath = d })
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            directories = new List<DirectoryEntryDto>();
        }

        var parentPath = Directory.GetParent(normalized)?.FullName;

        return Task.FromResult(new DirectoryBrowseDto
        {
            CurrentPath = normalized,
            ParentPath = parentPath,
            Directories = directories,
        });
    }

    private static IEnumerable<DriveInfo> GetAllowedDrives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady);

    // Only allows browsing within one of this machine's own fixed drives - defends against a
    // crafted path (e.g. a UNC path or one built from ".." segments) resolving somewhere
    // outside the set of roots the picker itself ever offers.
    private static string ValidateAndNormalizePath(string path)
    {
        if (!Path.IsPathRooted(path))
        {
            throw new BusinessValidationException("This folder is not accessible.");
        }

        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch
        {
            throw new BusinessValidationException("This folder is not accessible.");
        }

        var root = Path.GetPathRoot(full)?.TrimEnd('\\');
        var isAllowedDrive = !string.IsNullOrEmpty(root) &&
            GetAllowedDrives().Any(d => string.Equals(d.Name.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase));

        if (!isAllowedDrive || !Directory.Exists(full))
        {
            throw new BusinessValidationException("This folder is not accessible.");
        }

        return full;
    }

    private Task<FetchRun?> GetLatestHistoryAsync(CancellationToken cancellationToken) =>
        _context.FetchRuns
            .Where(h => h.RecordKind == FetchRunRecordKinds.PathChange)
            .OrderByDescending(h => h.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static RootPathHistoryItemDto MapToHistoryItem(FetchRun h) => new()
    {
        Id = h.Id,
        ModifiedAt = h.StartedAt,
        FromPath = h.FromPath,
        ToPath = h.SourcePath,
        ModifiedBy = h.ExecutedByUsername,
        Remarks = h.Remarks,
    };

    private static FetchRunItemDto MapToFetchRunItem(FetchRun r) => new()
    {
        Id = r.Id,
        StartedAt = r.StartedAt,
        CompletedAt = r.CompletedAt,
        RunTime = FormatRunTime(r.StartedAt, r.CompletedAt),
        ProcessedCount = r.ProcessedCount ?? 0,
        TotalCount = r.TotalCount,
        Status = r.Status?.ToString() ?? string.Empty,
        ExecutedBy = r.ExecutedByUsername,
        SourcePath = r.SourcePath,
    };

    private static string? FormatRunTime(DateTime startedAt, DateTime? completedAt)
    {
        if (completedAt is null)
        {
            return null;
        }

        var span = completedAt.Value - startedAt;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m {span.Seconds}s"
            : span.TotalMinutes >= 1
                ? $"{span.Minutes}m {span.Seconds}s"
                : $"{span.Seconds}s";
    }
}
