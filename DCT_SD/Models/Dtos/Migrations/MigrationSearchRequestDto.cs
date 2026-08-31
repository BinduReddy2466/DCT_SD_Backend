using DCT_SD.Services;

namespace DCT_SD.Models.Dtos.Migrations;

public class MigrationSearchRequestDto : IPageableRequest
{
    public string? RdCode { get; set; }
    public string? RequestNumber { get; set; }
    public string? EntryNumbersCsv { get; set; }
    public string? Title { get; set; }
    public string? MigrationStatus { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
