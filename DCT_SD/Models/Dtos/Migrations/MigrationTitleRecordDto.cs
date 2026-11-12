namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationTitleRecordDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? TitleSequence { get; set; }
    public string? MigrationType { get; set; }
    public string MigrationStatus { get; set; } = string.Empty;
    public string? TargetSystemValidation { get; set; }
    public string? ReasonForFailing { get; set; }
}
