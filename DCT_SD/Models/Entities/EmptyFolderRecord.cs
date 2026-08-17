namespace DCT_SD.Models.Entities;

// Field shape mirrors the Empty Entry Folders grid in the frontend. Population of this table
// depends on the (not yet built) extraction pipeline - see RdConfigService.StartFetchAsync.
// Status is intentionally a free-text column rather than an enum: the frontend only ever shows
// a single literal value ("Empty Entry Folder") and no other status has been specified.
public class EmptyFolderRecord
{
    public int Id { get; set; }
    public string? RdCode { get; set; }
    public string? RdName { get; set; }
    public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime FetchDateTime { get; set; }
}
