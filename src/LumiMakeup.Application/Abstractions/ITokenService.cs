using LumiMakeup.Domain.Entities;

namespace LumiMakeup.Application.Abstractions;

public interface ITokenService
{
    (string TokenAcesso, string TokenRefresh) GerarTokens(Usuario usuario);
    string? ObterIdDeUsuarioDoTokenRefresh(string tokenRefresh);

    /// <summary>
    /// Id e e-mail do refresh, para amarrar a renovação à pessoa — e não só ao
    /// número do id, que pode ter sido reaproveitado depois de um reset.
    /// Nulo quando o token é inválido, expirou ou não é refresh.
    /// </summary>
    (string Sub, string? Email)? ObterIdentidadeDoTokenRefresh(string tokenRefresh);
}

public interface IAutenticacaoGoogleService
{
    Task<DadosDoUsuarioGoogle?> ValidarTokenIdAsync(string tokenId, CancellationToken cancellationToken = default);
}

public sealed record DadosDoUsuarioGoogle(string IdGoogle, string Email, string Nome);