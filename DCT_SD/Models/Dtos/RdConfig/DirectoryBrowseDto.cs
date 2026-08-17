namespace DCT_SD.Models.Dtos.RdConfig;

// CurrentPath is null at the top level (the drive list) - there is no single "selected folder"
// yet at that level, so the modal's Select button stays disabled until the admin drills into
// an actual drive/folder.
public class DirectoryBrowseDto
{
    public string? CurrentPath { get; set; }
    public string? ParentPath { get; set; }
    public IReadOnlyList<DirectoryEntryDto> Directories { get; set; } = Array.Empty<DirectoryEntryDto>();
    public string? Error { get; set; }
}
