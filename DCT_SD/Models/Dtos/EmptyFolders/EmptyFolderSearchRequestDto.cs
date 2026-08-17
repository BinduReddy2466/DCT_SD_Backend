namespace DCT_SD.Models.Dtos.EmptyFolders;

public class EmptyFolderSearchRequestDto
{
    public string? RdCode { get; set; }
    public string? FolderName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
