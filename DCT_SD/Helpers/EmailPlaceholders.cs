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
            ["{{CurrentDate}}"] = DateTime.Now.ToString("MM-dd-yyyy"),
        };

        var result = text;
        foreach (var (placeholder, value) in sample)
        {
            result = result.Replace(placeholder, value);
        }

        return result;
    }
}
