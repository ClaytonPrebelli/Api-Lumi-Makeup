using Google.Apis.Auth;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Security;

public sealed class AutenticacaoGoogleOptions
{
    public string IdCliente { get; set; } = string.Empty;
}

public sealed class AutenticacaoGoogleService : IAutenticacaoGoogleService
{
    private readonly AutenticacaoGoogleOptions _opcoes;

    public AutenticacaoGoogleService(IOptions<AutenticacaoGoogleOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public async Task<DadosDoUsuarioGoogle?> ValidarTokenIdAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.IdCliente))
        {
            throw new InvalidOperationException("IdCliente do Google não configurado (ExternalServices:Google:IdCliente).");
        }

        var configuracoes = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _opcoes.IdCliente }
        };

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(tokenId, configuracoes);

            return new DadosDoUsuarioGoogle(
                payload.Subject,
                payload.Email,
                payload.Name ?? string.Empty);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}