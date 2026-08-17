namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationListItemDto
{
    public int Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public DateTime MigrationDate { get; set; }
    public string RdCode { get; set; } = string.Empty;
    public string RdName { get; set; } = string.Empty;
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? TitleType { get; set; }
    public string MigrationStatus { get; set; } = string.Empty;
    public string SdStatus { get; set; } = string.Empty;
    public string MigratedTo { get; set; } = string.Empty;
}
