namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationDocumentDto
{
    public int Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ExistingFileName { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime? ActionDate { get; set; }
}
