namespace DCT_SD.Services;

// Implemented by the existing per-module search request DTOs (they already have exactly these
// two properties) so ReportService can page through all of a module's matching records via its
// own SearchAsync method without needing type-specific code per report.
public interface IPageableRequest
{
    int PageNumber { get; set; }
    int PageSize { get; set; }
}
