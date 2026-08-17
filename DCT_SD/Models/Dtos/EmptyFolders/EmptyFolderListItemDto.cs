namespace DCT_SD.Models.Dtos.EmptyFolders;

public class EmptyFolderListItemDto
{
    public int Id { get; set; }
    public DateTime FetchDateTime { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
