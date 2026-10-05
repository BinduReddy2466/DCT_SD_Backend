using DCT_SD.Models.Dtos.Auth;

namespace DCT_SD.Services;

public interface ITokenService
{
    /// How long a password-reset link stays valid (Jwt:PasswordResetMinutes, default 30) -
    /// exposed so callers that need to tell the user the expiry (e.g. the Forgot Password email
    /// body) read it from the one place it's actually configured, instead of duplicating the
    /// same config key/fallback logic in a second spot.
    int PasswordResetMinutes { get; }

    /// Signs a short-lived access token. The payload carries only what authorization needs
    /// (user id, username, role, one claim per allowed menu key) - no password hash, no
    /// other PII - since a JWT's payload is base64-encoded, not encrypted, and must be
    /// treated as readable by anyone who holds the token.
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(int userId, string username, string role, IEnumerable<string> menuKeys);

    /// A cryptographically random opaque value - never a JWT - so a refresh token carries no
    /// inspectable claims of its own; it is only ever a lookup key into the RefreshTokens table.
    string CreateRefreshTokenValue();

    /// SHA-256 hex digest. Only this hash is ever persisted, so a stolen database backup does
    /// not hand over usable session tokens.
    string HashRefreshToken(string rawToken);

    /// A short-lived, purpose-scoped JWT for the "set your password" / "reset your password"
    /// link emailed to a user - reuses the same signing key as the login access token (no new
    /// database table needed to track reset tokens) but is never accepted as a login token
    /// itself, since it carries a distinct "purpose" claim ValidatePasswordResetToken checks
    /// for. The email is embedded as a claim (not looked up again from a userId at validation
    /// time) purely so the Reset Password page can display it without a round trip.
    (string Token, DateTime ExpiresAtUtc) CreatePasswordResetToken(int userId, string email);

    /// Validates a password-reset token (signature, issuer/audience, that it actually is one of
    /// these tokens and not a login access token) and separately reports whether an otherwise-
    /// genuine token simply expired, since the Reset/Forgot Password pages show a distinct exact
    /// message for that one case versus every other invalid-token reason.
    PasswordResetTokenValidation ValidatePasswordResetToken(string token);
}
