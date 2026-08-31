namespace DCT_SD.Models.Dtos.Reports;

// Generic table shape for the Reports page's live preview - one row of formatted display
// strings per record, so a single partial view can render any report type without needing to
// know its underlying columns.
public class ReportPreviewDto
{
    public IReadOnlyList<string> Headers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = Array.Empty<IReadOnlyList<string>>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
