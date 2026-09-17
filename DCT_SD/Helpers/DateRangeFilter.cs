namespace DCT_SD.Helpers;

// Shared helper for every "Date From / Date To" search filter in the app (Fetch History, Root
// Source Path Update History, Manual Validation, Empty Folders, Failed Extraction, Migration
// Monitoring, User Management - and Reports, which reuses each of those modules' own search
// logic rather than re-implementing filtering). An <input type="date"> Date To value always
// binds to midnight (00:00:00) of the selected day, so a plain "<= DateTo" comparison against a
// full DateTime column only matches that exact instant and silently excludes every other record
// from the rest of that day. EndOfDayExclusive returns the start of the NEXT day, so callers
// filter with `column < EndOfDayExclusive(dateTo)` for a correctly inclusive "through end of the
// selected day" range - Date From needs no equivalent helper, since ">= DateFrom" already
// includes the whole day starting from that same midnight instant.
public static class DateRangeFilter
{
    public static DateTime EndOfDayExclusive(DateTime dateTo) => dateTo.Date.AddDays(1);
}
