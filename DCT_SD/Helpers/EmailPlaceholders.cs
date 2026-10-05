namespace DCT_SD.Helpers;

// Ports fillEmailPlaceholders from the React frontend verbatim - substitutes the same sample
// values so a template preview shows realistic-looking output without a real send event.
public static class EmailPlaceholders
{
    public static string Fill(string text)
    {
        var sample = new Dictionary<string, string>
        {
            ["{{FirstName}}"] = "Jane",
            ["{{LastName}}"] = "Doe",
            ["{{Email}}"] = "jane@gmail.com",
            ["{{TemporaryPassword}}"] = "TempPass@123",
            ["{{ResetPasswordLink}}"] = "https://lares.example.com/reset-password?token=demo",
            ["{{ChangePasswordLink}}"] = "https://lares.example.com/change-password?token=demo",
            ["{{ExpiryMinutes}}"] = "30",
            ["{{CurrentDate}}"] = DateTime.UtcNow.ToLocalDisplay().ToString("MM-dd-yyyy"),
        };

        var result = text;
        foreach (var (placeholder, value) in sample)
        {
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    // The real-send counterpart to Fill() above: substitutes the actual values for a specific
    // account/event instead of the fixed preview sample. Any placeholder with no matching entry
    // in `values` is left in the text as-is, rather than silently dropped, so a typo'd/unused
    // placeholder in an admin-edited template is obvious in the sent email instead of just
    // vanishing.
    public static string FillWithValues(string text, IReadOnlyDictionary<string, string> values)
    {
        var result = text;
        foreach (var (placeholder, value) in values)
        {
            result = result.Replace(placeholder, value);
        }

        return result;
    }
}
