using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LumiMakeup.Infrastructure.Security;

public sealed class TokenJwtOptions
{
    public string Segredo { get; set; } = string.Empty;
    public string Emissor { get; set; } = string.Empty;
    public string Audiencia { get; set; } = string.Empty;
    public int MinutosDeExpiracao { get; set; } = 60;
    public int DiasDeExpiracaoDoRefresh { get; set; } = 7;
}

public sealed class JwtTokenService : ITokenService
{
    private readonly TokenJwtOptions _opcoes;

    public JwtTokenService(IOptions<TokenJwtOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public (string TokenAcesso, string TokenRefresh) GerarTokens(Usuario usuario)
    {
        var chaveAssinatura = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Segredo));
        var credenciais = new SigningCredentials(chaveAssinatura, SecurityAlgorithms.HmacSha256);

        var claimsDeAcesso = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nome),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Papel.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Typ, "access")
        };

        var tokenDeAcesso = new JwtSecurityToken(
            _opcoes.Emissor,
            _opcoes.Audiencia,
            claims: claimsDeAcesso,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_opcoes.MinutosDeExpiracao),
            signingCredentials: credenciais);

        var claimsDeRefresh = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Typ, "refresh")
        };

        var tokenDeRefresh = new JwtSecurityToken(
            _opcoes.Emissor,
            _opcoes.Audiencia,
            claims: claimsDeRefresh,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddDays(_opcoes.DiasDeExpiracaoDoRefresh),
            signingCredentials: credenciais);

        return (
            new JwtSecurityTokenHandler().WriteToken(tokenDeAcesso),
            new JwtSecurityTokenHandler().WriteToken(tokenDeRefresh));
    }

    public string? ObterIdDeUsuarioDoTokenRefresh(string tokenRefresh)
    {
        try
        {
            var manipuladorDeToken = new JwtSecurityTokenHandler();
            var parametrosDeValidacao = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Segredo)),
                ValidateIssuer = true,
                ValidIssuer = _opcoes.Emissor,
                ValidateAudience = true,
                ValidAudience = _opcoes.Audiencia,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var principal = manipuladorDeToken.ValidateToken(tokenRefresh, parametrosDeValidacao, out _);
            var tipo = principal.FindFirst(JwtRegisteredClaimNames.Typ)?.Value;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return tipo == "refresh" ? sub : null;
        }
        catch
        {
            return null;
        }
    }
}