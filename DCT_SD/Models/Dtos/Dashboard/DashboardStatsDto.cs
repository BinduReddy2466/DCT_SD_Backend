using DCT_SD.Models;

namespace DCT_SD.Models.Dtos.Dashboard;

// AllRows is every RD's Folder+Images rows, unfiltered and unpaged - the KPI cards and charts on
// the Dashboard page always summarize across this, regardless of the table's own RD Code filter
// or current page, so the top-of-page summary never changes just because the user paged/filtered
// the table below it. Table is the same underlying rows, filtered to one RD Code (if provided)
// and paged by RD count (PageSize RDs per page = PageSize*2 rows per page, one Folder + one
// Images row per RD) for the statistics table itself.
public class DashboardStatsDto
{
    public IReadOnlyList<DashboardRowDto> AllRows { get; set; } = Array.Empty<DashboardRowDto>();
    public PagedResult<DashboardRowDto> Table { get; set; } = new();
}
