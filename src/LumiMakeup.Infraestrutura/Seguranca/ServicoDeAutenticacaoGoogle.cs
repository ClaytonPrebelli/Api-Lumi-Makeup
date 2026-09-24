using Google.Apis.Auth;
using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infraestrutura.Seguranca;

public sealed class OpcoesDeAutenticacaoGoogle
{
    public string IdCliente { get; set; } = string.Empty;
}

public sealed class ServicoDeAutenticacaoGoogle : IServicoDeAutenticacaoGoogle
{
    private readonly OpcoesDeAutenticacaoGoogle _opcoes;

    public ServicoDeAutenticacaoGoogle(IOptions<OpcoesDeAutenticacaoGoogle> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    public async Task<DadosDoUsuarioGoogle?> ValidarTokenIdAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.IdCliente))
        {
            throw new InvalidOperationException("IdCliente do Google não configurado (ServicosExternos:Google:IdCliente).");
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