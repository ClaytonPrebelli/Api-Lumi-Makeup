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
}
