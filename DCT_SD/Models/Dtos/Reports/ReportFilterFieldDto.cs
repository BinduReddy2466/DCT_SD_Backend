namespace DCT_SD.Models.Dtos.Reports;

// Describes one filter input the Reports page should render for a given report type, mirroring
// that report's corresponding module's own search form field-for-field.
public class ReportFilterFieldDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    // "text" | "date" | "select"
    public string Type { get; set; } = "text";
    public string? Placeholder { get; set; }
    public IReadOnlyList<ReportFilterOptionDto>? Options { get; set; }
}

public class ReportFilterOptionDto
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
