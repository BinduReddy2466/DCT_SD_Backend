namespace DCT_SD.Helpers;

// Every timestamp in this app is stored and computed as UTC (DateTime.UtcNow) - correct
// practice for the database. Display conversion deliberately does NOT target a specific,
// hardcoded timezone (e.g. Philippine Time) - it follows whatever timezone the server/VM this
// app is actually running on is configured with, via .NET's own DateTime.ToLocalTime() (which
// reads the OS's configured local timezone at runtime). Deploy this on a machine set to
// Philippine Time and it displays Philippine Time; deploy it anywhere else and it follows that
// machine's own configured timezone instead - nothing in this code needs to change either way.
// Call ToLocalDisplay() on any stored UTC value immediately before formatting it for display;
// never format a raw UtcNow-derived value directly.
public static class DisplayTime
{
    public static DateTime ToLocalDisplay(this DateTime utcValue)
    {
        var utc = utcValue.Kind == DateTimeKind.Utc ? utcValue : DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        return utc.ToLocalTime();
    }

    public static DateTime? ToLocalDisplay(this DateTime? utcValue) => utcValue?.ToLocalDisplay();
}
