using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

// Resolves mapped/shared network drives - and mapped-drive paths to their UNC form - dynamically
// from the current Windows process's own drive-mapping table (via the WNetGetConnection Win32
// API), never from a hardcoded drive letter or server. This is deliberately the ONLY thing this
// service knows about: which network drives THIS process's identity currently sees, and what
// they resolve to. On the local dev VM that's the interactively logged-on user; once deployed to
// IIS it's whatever the Application Pool's identity has mapped (which may be none at all - see
// GetMappedDrives, no drive is ever assumed present).
public class RemoteFolderBrowserService : IRemoteFolderBrowserService
{
    private readonly ILogger<RemoteFolderBrowserService> _logger;

    public RemoteFolderBrowserService(ILogger<RemoteFolderBrowserService> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<MappedDriveDto> GetMappedDrives()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Mapped drives are a Windows concept (WNetGetConnection is Windows-only); on any
            // other OS there simply are none to offer - not an error, just an empty picker.
            return Array.Empty<MappedDriveDto>();
        }

        var result = new List<MappedDriveDto>();

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Network)
            {
                // Local/fixed drives (C:, D:, ...) are never offered - mapped/shared drives only.
                continue;
            }

            var letter = drive.Name.TrimEnd('\\');
            if (!TryGetUncTarget(letter, out var uncPath))
            {
                // A network drive DriveInfo enumerated but WNetGetConnection couldn't resolve
                // (disconnected share, transient network issue, etc.) - skip it rather than show
                // a drive with no usable target; logged for diagnosis, never surfaced raw to the UI.
                _logger.LogWarning("Mapped drive {DriveLetter} is listed but its UNC target could not be resolved.", letter);
                continue;
            }

            result.Add(new MappedDriveDto { DriveLetter = letter, UncPath = uncPath });
        }

        return result;
    }

    public bool TryResolveToUncPath(string path, out string uncPath)
    {
        uncPath = string.Empty;

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // Already UNC - only accept it if its server+share root matches a currently mapped
            // drive's target, so browsing can never be pointed at an arbitrary network location
            // that just happens to be reachable, only ones actually offered by the picker itself.
            var mapped = GetMappedDrives();
            var match = mapped.FirstOrDefault(d =>
                path.Equals(d.UncPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(d.UncPath + @"\", StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                return false;
            }

            uncPath = path;
            return true;
        }

        if (path.Length < 2 || path[1] != ':')
        {
            return false;
        }

        var driveLetter = path[..2];
        var driveMatch = GetMappedDrives().FirstOrDefault(d => string.Equals(d.DriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase));
        if (driveMatch is null)
        {
            // Not a currently-mapped drive - includes every local/fixed drive (C:, D:, E:, ...),
            // which is exactly what keeps those out of reach here as well as out of the list.
            return false;
        }

        var remainder = path[2..].TrimStart('\\');
        uncPath = remainder.Length == 0 ? driveMatch.UncPath : $"{driveMatch.UncPath}\\{remainder}";
        return true;
    }

    [SupportedOSPlatform("windows")]
    private bool TryGetUncTarget(string driveLetter, out string uncPath)
    {
        uncPath = string.Empty;

        var length = 260;
        var buffer = new StringBuilder(length);
        var errorCode = WNetGetConnection(driveLetter, buffer, ref length);

        if (errorCode == ErrorMoreData)
        {
            // Buffer too small for this particular UNC path - the call reports the required size
            // in `length`; retry once with it rather than assuming a fixed maximum was enough.
            buffer = new StringBuilder(length);
            errorCode = WNetGetConnection(driveLetter, buffer, ref length);
        }

        if (errorCode != NoError)
        {
            if (errorCode != ErrorNotConnected)
            {
                _logger.LogWarning("WNetGetConnection({DriveLetter}) failed with error code {ErrorCode}.", driveLetter, errorCode);
            }

            return false;
        }

        // WNetGetConnection returns the server's hostname/NetBIOS name (e.g.
        // \\DEV2-IMCCO-IMG\Expediente) - resolved here to its IP address (e.g.
        // \\10.51.5.12\Expediente) so the UNC path is IP-based from this single point onward,
        // through browsing, Selected Path, and the saved database value alike. Never a hardcoded
        // IP - resolved fresh via DNS each time, so the same code works on any machine/network.
        uncPath = ResolveUncHostToIp(buffer.ToString());
        return true;
    }

    // Falls back to the original hostname-based UNC path (never throws/fails the whole browse)
    // if the host can't be resolved - e.g. a transient DNS/network issue - so a resolution
    // hiccup degrades to the old behavior rather than hiding the drive entirely.
    private string ResolveUncHostToIp(string uncPath)
    {
        if (!uncPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return uncPath;
        }

        var rest = uncPath[2..];
        var slashIndex = rest.IndexOf('\\');
        var host = slashIndex < 0 ? rest : rest[..slashIndex];
        var afterHost = slashIndex < 0 ? string.Empty : rest[slashIndex..];

        if (IPAddress.TryParse(host, out _))
        {
            // Already an IP - nothing to resolve.
            return uncPath;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(host);
            var resolved = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
            if (resolved is not null)
            {
                return $@"\\{resolved}{afterHost}";
            }

            _logger.LogWarning("DNS lookup for UNC host {Host} returned no addresses; using the hostname as-is.", host);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            _logger.LogWarning(ex, "Could not resolve UNC host {Host} to an IP address; using the hostname as-is.", host);
        }

        return uncPath;
    }

    private const int NoError = 0;
    private const int ErrorMoreData = 234;
    private const int ErrorNotConnected = 2250;

    [SupportedOSPlatform("windows")]
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(
        [MarshalAs(UnmanagedType.LPWStr)] string localName,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder remoteName,
        ref int length);
}
