using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LumiMakeup.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
    public int RefreshExpirationDays { get; set; } = 7;
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string AccessToken, string RefreshToken) GenerateTokens(User user)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var accessClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Typ, "access")
        };

        var accessToken = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims: accessClaims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            signingCredentials: credentials);

        var refreshClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Typ, "refresh")
        };

        var refreshToken = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims: refreshClaims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(_options.RefreshExpirationDays),
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(accessToken),
            new JwtSecurityTokenHandler().WriteToken(refreshToken));
    }

    public string? GetUserIdFromRefreshToken(string refreshToken)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = tokenHandler.ValidateToken(refreshToken, validationParameters, out _);
            var typ = principal.FindFirst(JwtRegisteredClaimNames.Typ)?.Value;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return typ == "refresh" ? sub : null;
        }
        catch
        {
            return null;
        }
    }
}