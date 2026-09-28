namespace DCT_SD.Models.Dtos.RdConfig;

// One mapped/shared network drive visible to the current process's Windows identity (the
// interactive VM user when run locally, or the IIS Application Pool identity when deployed) -
// never a hardcoded drive letter/server. See IRemoteFolderBrowserService.
public class MappedDriveDto
{
    public string DriveLetter { get; set; } = string.Empty;
    public string UncPath { get; set; } = string.Empty;
}
