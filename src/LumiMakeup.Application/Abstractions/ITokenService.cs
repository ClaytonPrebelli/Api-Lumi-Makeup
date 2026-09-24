using LumiMakeup.Domain.Entities;

namespace LumiMakeup.Application.Abstractions;

public interface ITokenService
{
    (string TokenAcesso, string TokenRefresh) GerarTokens(Usuario usuario);
    string? ObterIdDeUsuarioDoTokenRefresh(string tokenRefresh);
}

public interface IAutenticacaoGoogleService
{
    Task<DadosDoUsuarioGoogle?> ValidarTokenIdAsync(string tokenId, CancellationToken cancellationToken = default);
}

public sealed record DadosDoUsuarioGoogle(string IdGoogle, string Email, string Nome);