namespace DCT_SD.Models.Dtos.Reports;

public class ReportGenerationResult
{
    public bool HasRecords { get; set; }
    public byte[]? FileBytes { get; set; }
    public string FileName { get; set; } = string.Empty;
}
