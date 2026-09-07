namespace DCT_SD.Helpers;

// Every timestamp in this app is stored and computed as UTC (DateTime.UtcNow) - correct
// practice for the database. But every view was formatting that raw UTC value directly as if
// it were already local time, so it displayed 5.5 hours behind India Standard Time for every
// user. This is a single-tenant internal tool (Paradigm IT, India), so there is no per-user
// timezone preference to honor - IST is simply the one timezone this app's users are in. Call
// ToIst() on any stored UTC value immediately before formatting it for display; never format a
// raw UtcNow-derived value directly.
public static class DisplayTime
{
    private static readonly TimeZoneInfo IndiaTimeZone = ResolveIndiaTimeZone();

    public static DateTime ToIst(this DateTime utcValue)
    {
        var utc = utcValue.Kind == DateTimeKind.Utc ? utcValue : DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, IndiaTimeZone);
    }

    public static DateTime? ToIst(this DateTime? utcValue) => utcValue?.ToIst();

    // Windows and Linux/ICU use different timezone ID conventions for the same zone.
    private static TimeZoneInfo ResolveIndiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); // Windows ID
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); // IANA ID
        }
    }
}
