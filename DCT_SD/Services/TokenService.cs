using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DCT_SD.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace DCT_SD.Services;

public class TokenService : ITokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenMinutes;

    public TokenService(IConfiguration configuration)
    {
        var signingKeyValue = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyValue));
        _issuer = configuration["Jwt:Issuer"] ?? "DCT_SD";
        _audience = configuration["Jwt:Audience"] ?? "DCT_SD";
        _accessTokenMinutes = int.TryParse(configuration["Jwt:AccessTokenMinutes"], out var minutes) ? minutes : 15;
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
}
