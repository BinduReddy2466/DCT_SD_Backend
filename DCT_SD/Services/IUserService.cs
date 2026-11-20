using DCT_SD.Models;
using DCT_SD.Models.Dtos.Users;

namespace DCT_SD.Services;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> SearchAsync(UserSearchRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// Case-insensitive username (email) lookup for Forgot Password - returns null rather than
    /// throwing when nothing matches, since "not found" is an expected, routine outcome here
    /// (the page shows "the email address provided is not valid"), not an error condition.
    Task<UserDetailDto?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<UserDetailDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default);
    Task<UserDetailDto> UpdateAsync(int id, UpdateUserRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// Sets a user's password directly (no current-password check) - used by the Reset Password
    /// flow (proof of identity is the signed, short-lived reset token itself - see
    /// ITokenService.ValidatePasswordResetToken, not a known current password) and by account
    /// reactivation's system-generated temporary password. requireChangeOnNextLogin controls
    /// whether this leaves MustChangePassword set: true for a temporary password the user never
    /// chose themselves, false for a password the user just picked on their own (self-service
    /// reset already satisfies "change your password").
    Task ResetPasswordAsync(int userId, string newPassword, bool requireChangeOnNextLogin, CancellationToken cancellationToken = default);

    /// The Change Password page's own flow: the user proves identity with their current
    /// password (unlike ResetPasswordAsync above) and always clears MustChangePassword on
    /// success, since completing this page is exactly what that flag is waiting for.
    Task ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken cancellationToken = default);
}
