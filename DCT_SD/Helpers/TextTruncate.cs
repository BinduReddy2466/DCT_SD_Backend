namespace DCT_SD.Helpers;

// Shortens a long, unbroken value - e.g. a comma-separated EntryNumberRange that can run to
// hundreds of entries - to a fixed character length for display in a list/table cell, so a
// single value can never wrap onto enough lines to blow a row out to fill the page. The full
// value is never lost: callers pair this with a `title` attribute carrying the untruncated text,
// so it's still available on hover, and the Details page always shows the value in full.
public static class TextTruncate
{
    public static string ForDisplay(string value, int maxLength = 80) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";
}
