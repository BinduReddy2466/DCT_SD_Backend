using DCT_SD.Models.Dtos.Dashboard;

namespace DCT_SD.Services;

public interface IDashboardService
{
    Task<IReadOnlyList<DashboardRowDto>> GetStatsAsync(CancellationToken cancellationToken = default);
}
