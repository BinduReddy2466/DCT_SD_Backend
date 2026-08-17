using System.Text.RegularExpressions;

namespace DCT_SD.Helpers;

// Ports the frontend's userValidation.ts regex verbatim (manual validation, not
// DataAnnotations, since "required" is conditional - only on create for Username/Password).
public static class UserValidation
{
    private static readonly Regex UsernamePattern = new(
        @"^[A-Za-z0-9](?:[A-Za-z0-9._]*[A-Za-z0-9])?@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$",
        RegexOptions.Compiled);

    public static bool IsValidUsername(string username) => UsernamePattern.IsMatch(username);

    public static bool IsValidPassword(string password)
    {
        if (Regex.IsMatch(password, "[ñÑ]")) return false;
        if (password.Length < 8 || password.Length > 32) return false;
        if (!Regex.IsMatch(password, "[A-Z]")) return false;
        if (!Regex.IsMatch(password, "[a-z]")) return false;
        if (!Regex.IsMatch(password, "[0-9]")) return false;
        if (!Regex.IsMatch(password, @"[!@#$%^&*_\-+=]")) return false;
        return true;
    }

    public static readonly string[] UsernameRequirements =
    {
        "The email address must follow a standard format: local-part@domain.extension",
        "Example: user@example.com",
        "Allowed characters: letters (a-z, A-Z), digits (0-9), period (.), underscore (_)",
        "ñ and Ñ are not acceptable.",
    };

    public static readonly string[] PasswordRequirements =
    {
        "Should be 8 to 32 characters long.",
        "Must contain at least one uppercase letter, one lowercase letter, one number, and one special character (!,@#$%^&*_-+=).",
        "ñ and Ñ are not acceptable.",
    };
}
