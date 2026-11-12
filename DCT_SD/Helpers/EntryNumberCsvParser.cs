namespace DCT_SD.Helpers;

// Splits a comma-separated Entry Number list (MigrationRecords.EntryNumbersCsv's source value) into
// its individual entry numbers, so MigrationRecord creation can produce one row per entry number
// instead of one row per source record (client requirement, 2026-11-12: "Migration Tracking should
// be per entry number").
public static class EntryNumberCsvParser
{
    // Trims each part, drops empties, and de-duplicates (preserving first-seen order) - guards
    // against a malformed source CSV like "5678,5678,9087" producing two rows with the same
    // (RequestNumber, EntryNumbersCsv) key, which would otherwise violate
    // MigrationRecordConfiguration's unique index. When nothing survives (null/blank input, or a
    // string with no real entry numbers in it), returns a single-element list holding the original
    // value as-is, so a source record with no captured entry numbers still produces exactly one
    // MigrationRecord row - unchanged from today's behavior for that edge case.
    public static IReadOnlyList<string?> Split(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [csv];
        }

        var parts = csv.Split(',')
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .Distinct()
            .ToList();

        return parts.Count > 0 ? parts! : [csv];
    }
}
