using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DCT_SD.Models;
using DCT_SD.Models.Dtos.Auth;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace DCT_SD.Services;

public class TokenService : ITokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;
    private readonly int _passwordResetMinutes;

    public int PasswordResetMinutes => _passwordResetMinutes;

    private const string PasswordResetPurpose = "password_reset";

    public TokenService(IConfiguration configuration)
    {
        var signingKeyValue = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
        _issuer = configuration["Jwt:Issuer"] ?? "DCT_SD";
        _audience = configuration["Jwt:Audience"] ?? "DCT_SD";
        _accessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 15;
        _passwordResetMinutes = int.TryParse(configuration["Jwt:PasswordResetMinutes"], out var resetMinutes) ? resetMinutes : 30;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(int userId, string username, string role, IEnumerable<string> menuKeys)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(menuKeys.Select(key => new Claim(AppClaimTypes.Menu, key)));

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expiresAt, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string CreateRefreshTokenValue()
    {
        // Hex, not Base64 - a cookie value containing '+', '/', or '=' is not reliably
        // round-tripped by every browser/proxy, which would silently corrupt the token on its
        // way back and make every rotation attempt fail the hash lookup.
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToHexString(bytes);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    public (string Token, DateTime ExpiresAtUtc) CreatePasswordResetToken(int userId, string email)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_passwordResetMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new("purpose", PasswordResetPurpose),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expiresAt, signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static readonly PasswordResetTokenValidation Invalid = new(false, false, null, null);

    public PasswordResetTokenValidation ValidatePasswordResetToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            // Otherwise-genuine token (right signature/issuer/audience), just past its expiry -
            // the one failure reason the Reset/Forgot Password pages show a distinct message for.
            return new PasswordResetTokenValidation(false, true, null, null);
        }
        catch
        {
            // Missing, tampered, or otherwise malformed - the caller shows a generic
            // "link is invalid" message, the specific reason is never surfaced.
            return Invalid;
        }

        // Without this check, a still-valid login access token (same signing key/issuer/audience)
        // would also pass validation above and let a logged-in session set an arbitrary user's
        // password - the purpose claim is what keeps these two token kinds from being
        // interchangeable even though they share one signing key.
        if (principal.FindFirstValue("purpose") != PasswordResetPurpose)
        {
            return Invalid;
        }

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Invalid;
        }

        var email = principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        return new PasswordResetTokenValidation(true, false, userId, email);
    }
}
