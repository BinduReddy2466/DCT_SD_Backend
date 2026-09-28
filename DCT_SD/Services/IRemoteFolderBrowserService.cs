using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

public interface IRemoteFolderBrowserService
{
    /// Enumerates the mapped/shared network drives currently visible to THIS process's Windows
    /// identity - never a static/hardcoded list. Local/fixed drives (C:, D:, ...) are never
    /// included. Returns an empty list (not an exception) when none are available - e.g. an IIS
    /// Application Pool identity with no network drive mapped for it.
    IReadOnlyList<MappedDriveDto> GetMappedDrives();

    /// Resolves a path to its UNC form: a mapped-drive-rooted path (e.g. "Z:\Source\2026")
    /// becomes "\\server\share\Source\2026"; a path already given as UNC is accepted only if its
    /// server+share root matches one of GetMappedDrives()'s current UNC roots (so an arbitrary
    /// UNC path can never be browsed to, only ones reachable through an actual mapped drive).
    /// Returns false - never throws - for anything else (a local drive, an unmapped/unknown
    /// root), so callers can turn that into a normal validation error.
    bool TryResolveToUncPath(string path, out string uncPath);
}
