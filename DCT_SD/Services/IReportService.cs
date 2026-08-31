using DCT_SD.Models.Dtos.Reports;

namespace DCT_SD.Services;

public interface IReportService
{
    Task<IReadOnlyList<ReportFilterFieldDto>> GetFilterFieldsAsync(string reportType, CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateAsync(string reportType, IDictionary<string, string?> filters, CancellationToken cancellationToken = default);
}
