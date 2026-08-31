using DCT_SD.Models.Dtos.Reports;

namespace DCT_SD.Services;

public interface IReportService
{
    Task<IReadOnlyList<ReportFilterFieldDto>> GetFilterFieldsAsync(string reportType, CancellationToken cancellationToken = default);

    // Paged, on-screen preview of the currently-matching records - same filters and columns as
    // the eventual Excel download, just formatted as display strings for the results table.
    Task<ReportPreviewDto> GetPreviewAsync(string reportType, IDictionary<string, string?> filters, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task<ReportGenerationResult> GenerateAsync(string reportType, IDictionary<string, string?> filters, CancellationToken cancellationToken = default);
}
