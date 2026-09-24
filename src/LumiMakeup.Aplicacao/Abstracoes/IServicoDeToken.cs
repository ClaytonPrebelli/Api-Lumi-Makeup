using LumiMakeup.Dominio.Entidades;

namespace LumiMakeup.Aplicacao.Abstracoes;

public interface IServicoDeToken
{
    (string TokenAcesso, string TokenRefresh) GerarTokens(Usuario usuario);
    string? ObterIdDeUsuarioDoTokenRefresh(string tokenRefresh);
}

public interface IServicoDeAutenticacaoGoogle
{
    Task<DadosDoUsuarioGoogle?> ValidarTokenIdAsync(string tokenId, CancellationToken cancellationToken = default);
}

public sealed record DadosDoUsuarioGoogle(string IdGoogle, string Email, string Nome);