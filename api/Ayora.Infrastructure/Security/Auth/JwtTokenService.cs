using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ayora.Application.Interfaces.Auth;
using Ayora.Application.Options.Auth;
using Ayora.Shared.Abstractions.Time;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ayora.Infrastructure.Security.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly ISystemClock _clock;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IOptions<JwtOptions> options, ISystemClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public (string AccessToken, DateTimeOffset ExpiresAt) CreateAccessToken(Guid userId, string email, IReadOnlyList<string> roles)
    {
        var expires = _clock.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email)
        };

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = CreateJwt(claims, expires.UtcDateTime);
        return (token, expires);
    }

    public (string ResetToken, DateTimeOffset ExpiresAt) CreatePasswordResetToken(Guid userId, string email)
    {
        var expires = _clock.UtcNow.AddMinutes(_options.PasswordResetTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("typ", "pwd_reset")
        };

        var token = CreateJwt(claims, expires.UtcDateTime);
        return (token, expires);
    }

    public bool TryValidatePasswordResetToken(string token, out Guid userId, out string email)
    {
        userId = default;
        email = string.Empty;

        var validation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            var principal = _handler.ValidateToken(token, validation, out var validatedToken);
            if (validatedToken is not JwtSecurityToken)
            {
                return false;
            }

            var type = principal.Claims.FirstOrDefault(c => c.Type == "typ")?.Value;
            if (!string.Equals(type, "pwd_reset", StringComparison.Ordinal))
            {
                return false;
            }

            var sub = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var em = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
            if (!Guid.TryParse(sub, out var id) || string.IsNullOrWhiteSpace(em))
            {
                return false;
            }

            userId = id;
            email = em;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string CreateJwt(IEnumerable<Claim> claims, DateTime expiresUtc)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: _clock.UtcNow.UtcDateTime,
            expires: expiresUtc,
            signingCredentials: creds);

        return _handler.WriteToken(jwt);
    }
}

