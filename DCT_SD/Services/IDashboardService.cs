using DCT_SD.Models.Dtos.Dashboard;

namespace DCT_SD.Services;

public interface IDashboardService
{
    // rdCode is an exact-match filter (from the RD Code dropdown/search box), applied only to the
    // paged Table - AllRows always covers every configured RD, unfiltered, for the KPI
    // cards/charts above the table.
    Task<DashboardStatsDto> GetStatsAsync(string? rdCode, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
