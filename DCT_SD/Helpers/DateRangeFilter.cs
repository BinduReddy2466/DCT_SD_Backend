namespace DCT_SD.Helpers;

// Shared helper for every "Date From / Date To" search filter in the app (Fetch History, Root
// Source Path Update History, Manual Validation, Empty Folders, Failed Extraction, Migration
// Monitoring, User Management - and Reports, which reuses each of those modules' own search
// logic rather than re-implementing filtering). DateFrom/DateTo are bound from an
// <input type="date"> as a Kind-less "calendar day" value with no timezone attached, while every
// column filtered here is stored as UTC (see each entity's SYSUTCDATETIME()/DateTime.UtcNow).
// Rows are displayed via DisplayTime.ToLocalDisplay(), which converts stored UTC to whichever
// timezone the host OS is configured with - so "today" as shown on screen is a host-local day,
// not a UTC day. Comparing the raw, Kind-less picked date directly against UTC storage silently
// treats the picked date as if it were already UTC midnight, which only happens to match the
// host-local day boundary shown on screen when the host's local timezone IS UTC. On a host set to
// a different timezone (e.g. an IIS box several hours ahead of UTC, vs. a UTC-configured dev VM),
// the two boundaries diverge: a row whose local display already reads as the next day can still
// be UTC-side within the previous day's filter range, so it wrongly passes a "Date To" filter set
// to that previous day. StartOfDayUtc/EndOfDayExclusive convert the picked local calendar day to
// the equivalent UTC instant (the same local-timezone basis ToLocalDisplay already uses, via
// DateTime.ToLocalTime()/TimeZoneInfo.Local) before comparing, so filtering and display agree on
// what day a row falls on regardless of which timezone the host happens to be set to.
public static class DateRangeFilter
{
    public static DateTime StartOfDayUtc(DateTime dateFrom) =>
        DateTime.SpecifyKind(dateFrom.Date, DateTimeKind.Local).ToUniversalTime();

    public static DateTime EndOfDayExclusive(DateTime dateTo) =>
        DateTime.SpecifyKind(dateTo.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
}
