using DCT_SD.Models.Dtos.Auth;
using DCT_SD.Models.Entities;

namespace DCT_SD.Services;

public interface IAuthService
{
    Task<User> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ResolveAllowedMenusAsync(User user, CancellationToken cancellationToken = default);
    Task<AuthenticatedUserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);

    /// A lean lookup (no menu resolution) used only for shell UI display (sidebar name/initials) -
    /// full name never travels in the JWT itself, so the layout fetches it fresh per page load.
    Task<(string FirstName, string LastName)?> GetDisplayNameAsync(int userId, CancellationToken cancellationToken = default);

    /// Issues a brand-new rolling refresh token for a user who just logged in (no prior token to rotate away).
    Task<(string RawToken, DateTime ExpiresAtUtc)> IssueRefreshTokenAsync(int userId, string? createdByIp, CancellationToken cancellationToken = default);

    /// Validates the presented refresh token, revokes it, and issues its replacement in the same
    /// operation (rotation). Presenting a token that was already rotated away is treated as
    /// possible theft/replay and revokes every active token the user holds.
    Task<RefreshRotationResult> RotateRefreshTokenAsync(string rawToken, string? createdByIp, CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default);
}
