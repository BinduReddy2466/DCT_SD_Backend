namespace DCT_SD.Services;

public interface ITokenService
{
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
}
