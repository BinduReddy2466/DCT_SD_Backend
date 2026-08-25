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
    private const string InvalidCredentialsMessage = "Invalid User ID or Password.";
    private const string AccountNotActiveMessage = "This account is locked/deactivated. Please contact your Administrator.";
    private const string InvalidSessionMessage = "Your session is no longer valid. Please log in again.";
    private const int MaxFailedLoginAttempts = 5;

    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly ITokenService _tokenService;
    private readonly int _refreshTokenDays;

    public AuthService(ApplicationDbContext context, ILogger<AuthService> logger, ITokenService tokenService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _tokenService = tokenService;
        _refreshTokenDays = int.TryParse(configuration["Jwt:RefreshTokenDays"], out var days) ? days : 7;
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

        if (user.Status != UserStatus.Active)
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
                _logger.LogWarning("User {UserId} auto-locked after {Attempts} failed login attempts", user.Id, user.FailedLoginAttempts);
            }
            await _context.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAppException(InvalidCredentialsMessage);
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
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays);

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
        var newExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays);
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
