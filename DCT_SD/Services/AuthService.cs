using DCT_SD.Configuration;
using DCT_SD.Helpers;
using DCT_SD.Helpers.Exceptions;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Auth;
using DCT_SD.Models.Entities;
using DCT_SD.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DCT_SD.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "The username or password you entered is incorrect. Please check and try again.";
    private const string AccountNotActiveMessage = "This account is locked/deactivated. Please contact your Administrator.";
    private const string AutoLockedMessage = "Too many failed login attempts. Your account has been temporarily locked. Please try again after 30 minutes.";
    private const string ConcurrentSessionMessage = "You are already logged in on another session. Please log out from the other session to continue.";
    private const string InvalidSessionMessage = "Your session is no longer valid. Please log in again.";
    private const int MaxFailedLoginAttempts = 3;
    private static readonly TimeSpan AutoLockoutDuration = TimeSpan.FromMinutes(30);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly ITokenService _tokenService;
    private readonly int _refreshTokenMinutes;

    public AuthService(ApplicationDbContext context, ILogger<AuthService> logger, ITokenService tokenService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _tokenService = tokenService;
        // A sliding 15-minute window, same length as the access token: as long as at least one
        // request comes in every 15 minutes, silent refresh (Program.cs middleware) keeps rotating
        // this forward and the session never visibly ends. No activity for 15+ minutes - including
        // the browser being closed outright, with no inactivity-timer JS left running to catch it -
        // and this row simply expires server-side, which is also what lets concurrent-session
        // prevention stop blocking a fresh login for that account.
        _refreshTokenMinutes = int.TryParse(configuration["Jwt:RefreshTokenMinutes"], out var minutes) ? minutes : 15;
    }

    public async Task<User> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var trimmed = username.Trim();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == trimmed.ToLower(), cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Login failed: unknown username {Username}", trimmed);
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
        }

        if (user.Status == UserStatus.Locked)
        {
            if (user.LockoutEndUtc.HasValue && user.LockoutEndUtc.Value <= DateTime.UtcNow)
            {
                // The 30-minute auto-lock window has elapsed - lift it here, at the moment it
                // actually matters (the next login attempt), rather than needing a background
                // job to sweep for expired lockouts. Falls through to the normal password check
                // below instead of blocking this attempt.
                user.Status = UserStatus.Active;
                user.LockoutEndUtc = null;
                user.FailedLoginAttempts = 0;
            }
            else
            {
                _logger.LogWarning("Login blocked for user {UserId}: account is locked.", user.Id);
                // LockoutEndUtc distinguishes an auto-lock (temporary, this exact message) from a
                // manual admin lock (LockoutEndUtc is never set for that path, stays locked
                // indefinitely, keeps the existing generic message).
                throw new UnauthorizedAppException(user.LockoutEndUtc.HasValue ? AutoLockedMessage : AccountNotActiveMessage);
            }
        }
        else if (user.Status != UserStatus.Active)
        {
            _logger.LogWarning("Login blocked for user {UserId}: status is {Status}", user.Id, user.Status);
            throw new UnauthorizedAppException(AccountNotActiveMessage);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.Status = UserStatus.Locked;
                user.LockoutEndUtc = DateTime.UtcNow.Add(AutoLockoutDuration);
                _logger.LogWarning("User {UserId} auto-locked for {Minutes} minutes after {Attempts} failed login attempts", user.Id, AutoLockoutDuration.TotalMinutes, user.FailedLoginAttempts);
            }
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
        }

        // Checked only once credentials are confirmed correct - a wrong-password attempt must
        // never reveal whether the account also happens to have an active session elsewhere, and
        // must still count toward the failed-attempt lockout above like any other wrong password.
        var hasActiveSession = await _context.RefreshTokens.AnyAsync(
            t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (hasActiveSession)
        {
            _logger.LogWarning("Login blocked for user {UserId}: an active session already exists.", user.Id);
            throw new UnauthorizedAppException(ConcurrentSessionMessage);
        }

        user.FailedLoginAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return user;
    }

    public Task<IReadOnlyList<string>> ResolveAllowedMenusAsync(User user, CancellationToken cancellationToken = default)
    {
        var explicitGrants = string.IsNullOrWhiteSpace(user.MenuPermissionsCsv)
            ? []
            : user.MenuPermissionsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Task.FromResult(AllowedMenuResolver.Resolve(user.RoleName, MenuKeys.BaseMenus, explicitGrants));
    }

    public async Task<AuthenticatedUserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        var allowedMenus = await ResolveAllowedMenusAsync(user, cancellationToken);
        return new AuthenticatedUserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Role = user.RoleName,
            AllowedMenus = allowedMenus,
        };
    }

    public async Task<(string FirstName, string LastName)?> GetDisplayNameAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        return user is null ? null : (user.FirstName, user.LastName);
    }

    public async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueRefreshTokenAsync(int userId, string? createdByIp, CancellationToken cancellationToken = default)
    {
        var rawToken = _tokenService.CreateRefreshTokenValue();
        var expiresAt = DateTime.UtcNow.AddMinutes(_refreshTokenMinutes);

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            TokenHash = _tokenService.HashRefreshToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CreatedByIp = createdByIp,
        });

        await _context.SaveChangesAsync(cancellationToken);
        return (rawToken, expiresAt);
    }

    public async Task<RefreshRotationResult> RotateRefreshTokenAsync(string rawToken, string? createdByIp, CancellationToken cancellationToken = default)
    {
        var hash = _tokenService.HashRefreshToken(rawToken);
        var existing = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            throw new UnauthorizedAppException(InvalidSessionMessage);
        }

        if (existing.RevokedAt is not null)
        {
            _logger.LogWarning("Refresh token reuse detected for user {UserId} - revoking all active sessions.", existing.UserId);
            await RevokeAllRefreshTokensForUserAsync(existing.UserId, cancellationToken);
            throw new UnauthorizedAppException(InvalidSessionMessage);
        }

        if (existing.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAppException(InvalidSessionMessage);
        }

        if (existing.User.Status != UserStatus.Active)
        {
            throw new UnauthorizedAppException(AccountNotActiveMessage);
        }

        var newRawToken = _tokenService.CreateRefreshTokenValue();
        var newExpiresAt = DateTime.UtcNow.AddMinutes(_refreshTokenMinutes);
        var newHash = _tokenService.HashRefreshToken(newRawToken);

        existing.RevokedAt = DateTime.UtcNow;
        existing.ReplacedByTokenHash = newHash;

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = newExpiresAt,
            CreatedByIp = createdByIp,
        });

        await _context.SaveChangesAsync(cancellationToken);

        var allowedMenus = await ResolveAllowedMenusAsync(existing.User, cancellationToken);

        return new RefreshRotationResult
        {
            User = existing.User,
            AllowedMenus = allowedMenus,
            NewRawToken = newRawToken,
            NewExpiresAtUtc = newExpiresAt,
        };
    }

    public async Task RevokeRefreshTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = _tokenService.HashRefreshToken(rawToken);
        var existing = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }

        existing.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
