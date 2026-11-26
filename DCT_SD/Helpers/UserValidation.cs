using System.Security.Cryptography;
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
        // The check above only confirms at least one allowed special character is present - it
        // never rejected a character OUTSIDE the allowed set (e.g. "Password@123?" was accepted
        // despite "?" not being one of !,@#$%^&*_-+=). This is the actual whole-string allow-list.
        if (!Regex.IsMatch(password, @"^[A-Za-z0-9!@#$%^&*_\-+=]+$")) return false;
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

    // System-generated password for account reactivation (Locked/Deactivated -> Active) - the
    // admin never types one in that flow, so it must satisfy IsValidPassword on its own.
    public static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*_-+=";
        const string all = upper + lower + digits + special;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            special[RandomNumberGenerator.GetInt32(special.Length)],
        };

        while (chars.Count < 12)
        {
            chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        }

        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }
}
