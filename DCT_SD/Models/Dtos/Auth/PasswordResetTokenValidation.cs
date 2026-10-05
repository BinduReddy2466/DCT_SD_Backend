namespace DCT_SD.Models.Dtos.Auth;

/// IsValid false + IsExpired true means the token is otherwise genuine (signature/issuer/
/// audience/purpose all check out) but past its expiry - the one case the Reset Password page
/// shows a distinct "this link has expired" message for, rather than the generic invalid-link
/// message used for every other failure reason (tampered, wrong purpose, malformed, missing).
public record PasswordResetTokenValidation(bool IsValid, bool IsExpired, int? UserId, string? Email);
