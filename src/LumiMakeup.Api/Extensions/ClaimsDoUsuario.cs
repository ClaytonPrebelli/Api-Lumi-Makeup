using System.Security.Claims;

namespace LumiMakeup.Api.Extensions;

public static class ClaimsDoUsuario
{
    /// <summary>
    /// Id do usuário do token, ou nulo quando o token não tem o claim.
    ///
    /// O id vem sempre do token e nunca do corpo da requisição. Uma rota que
    /// aceitasse o id do cliente no corpo permitiria criar pedido na conta de
    /// outra pessoa — inclusive registrar a venda de alguém e baixar o estoque
    /// dela.
    /// </summary>
    public static long? ObterId(this ClaimsPrincipal usuario)
    {
        var sub = usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// E-mail do token, ou nulo quando o token não tem o claim.
    ///
    /// Junto com o id, é o que amarra a sessão à pessoa: se o banco for
    /// recriado e o id passar a ser de outra pessoa, o e-mail não bate e a
    /// sessão cai — em vez de o pedido ir para a conta errada.
    /// </summary>
    public static string? ObterEmail(this ClaimsPrincipal usuario)
    {
        return usuario.FindFirst(ClaimTypes.Email)?.Value
            ?? usuario.FindFirst("email")?.Value;
    }
}
