using DCT_SD.Models.Enums;

namespace DCT_SD.Models.Entities;

public class User : AuditableEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? MenuPermissionsCsv { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
    public int FailedLoginAttempts { get; set; }

    /// Set only when Status became Locked automatically (3 consecutive failed attempts) - null
    /// for a manual admin lock, which is why AuthService.LoginAsync uses this field, not Status
    /// alone, to tell an auto-lock (auto-expires after 30 minutes) apart from an admin lock
    /// (stays locked until an admin changes it).
    public DateTime? LockoutEndUtc { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // True for a brand-new account (admin-set initial password) or one just reactivated with a
    // system-generated temporary password - cleared the moment the user successfully completes
    // the Change Password page. Checked on every request (Program.cs) to keep the whole app
    // locked to that one page until it's cleared.
    public bool MustChangePassword { get; set; }
}
