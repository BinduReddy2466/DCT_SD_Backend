using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using DCT_SD.Models.Dtos.RdConfig;

namespace DCT_SD.Services;

// Resolves mapped/shared network drives - and mapped-drive paths to their UNC form - dynamically
// from the current Windows process's own drive-mapping table (via the WNetGetConnection Win32
// API), never from a hardcoded drive letter or server. On the local dev VM that's the
// interactively logged-on user; once deployed to IIS it's whatever the Application Pool's
// identity has mapped - which, in practice, is often nothing at all, not due to misconfiguration
// but a genuine Windows limitation: a drive letter mapped for that identity in one logon session
// (e.g. a Scheduled Task run) is not visible from a different logon session for that same
// identity (IIS's own worker process) - Windows only auto-reconnects persistent drives during an
// actual interactive desktop logon, never for a background service. Live drive-letter detection
// therefore can't be relied on as the only source in an IIS deployment, so
// RemoteFolderBrowser:NetworkRoots (appsettings.json) supplements it with explicitly configured
// UNC paths - each entry is still exactly a network location, still subject to that Windows
// identity's real file-share permissions, and still resolved/validated the same way; this only
// removes the dependency on a drive letter happening to be visible in that specific session.
public class RemoteFolderBrowserService : IRemoteFolderBrowserService
{
    private readonly ILogger<RemoteFolderBrowserService> _logger;
    private readonly IConfiguration _configuration;

    public RemoteFolderBrowserService(ILogger<RemoteFolderBrowserService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public IReadOnlyList<MappedDriveDto> GetMappedDrives()
    {
        var result = new List<MappedDriveDto>();

        if (OperatingSystem.IsWindows())
        {
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
                    // (disconnected share, transient network issue, etc.) - skip it rather than
                    // show a drive with no usable target; logged for diagnosis, never surfaced
                    // raw to the UI.
                    _logger.LogWarning("Mapped drive {DriveLetter} is listed but its UNC target could not be resolved.", letter);
                    continue;
                }

                result.Add(new MappedDriveDto { DriveLetter = letter, UncPath = uncPath });
            }
        }

        foreach (var configuredRoot in _configuration.GetSection("RemoteFolderBrowser:NetworkRoots").Get<string[]>() ?? [])
        {
            if (string.IsNullOrWhiteSpace(configuredRoot)) continue;

            var normalized = configuredRoot.TrimEnd('\\');
            if (result.Any(d => string.Equals(d.UncPath, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue; // Already offered via a live-detected drive - don't list it twice.
            }

            // No drive letter - this entry only exists because a real mapped drive couldn't be
            // relied on to appear for this identity/session; the UI shows the UNC path directly.
            result.Add(new MappedDriveDto { DriveLetter = string.Empty, UncPath = normalized });
        }

        return result;
    }

    public bool TryResolveToUncPath(string path, out string uncPath)
    {
        uncPath = string.Empty;

        // No platform guard here - GetMappedDrives() already gates its own Windows-only portion
        // internally (drive-letter detection) while still returning configured NetworkRoots on
        // any OS, so this method works the same way: the UNC-already branch below never needed
        // Windows-specific APIs at all, and gating it out here would incorrectly block resolving
        // a configured network root too.
        if (string.IsNullOrWhiteSpace(path))
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
