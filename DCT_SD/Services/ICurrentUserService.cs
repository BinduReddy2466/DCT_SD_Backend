namespace DCT_SD.Services;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Username { get; }
    string? Role { get; }

    // "FirstName_LastName" of the currently logged-in user, resolved from the existing Users
    // table (never from a claim, never the login/Username value) - used wherever a record needs
    // to show/store who performed an action, in place of the raw Username/email. Null if there's
    // no logged-in user or their row can no longer be found.
    Task<string?> GetDisplayNameAsync(CancellationToken cancellationToken = default);
}
