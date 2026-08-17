namespace DCT_SD.Models.Entities;

// Rolling refresh tokens: each successful refresh revokes the presented token and issues a
// new one (RevokedAt + ReplacedByTokenHash record the rotation chain). If a token that has
// already been rotated away is presented again, that is treated as a signal of possible
// theft/replay and every active token for the user is revoked, forcing a fresh login on all
// sessions. Only the SHA-256 hash of the token is stored - the raw value only ever exists in
// the HttpOnly cookie and this row's owner can never be reconstructed from a DB read alone.
public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedByIp { get; set; }

    public User User { get; set; } = null!;
}
