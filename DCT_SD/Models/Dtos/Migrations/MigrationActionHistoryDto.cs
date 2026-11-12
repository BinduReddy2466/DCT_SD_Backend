namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationActionHistoryDto
{
    public DateTime ActionDate { get; set; }
    public string Activity { get; set; } = string.Empty;
    public string? MigrationType { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public string? Remarks { get; set; }
}
