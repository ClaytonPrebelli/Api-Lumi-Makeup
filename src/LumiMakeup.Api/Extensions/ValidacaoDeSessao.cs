using System.Security.Claims;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Api.Extensions;

/// <summary>
/// Amarra o token à pessoa, e não só ao número do id.
///
/// A assinatura sozinha não basta: se o banco for recriado, o id do token pode
/// passar a ser de outra pessoa, e um pedido feito com a sessão antiga cairia
/// na conta errada. Conferindo id + e-mail contra o banco a cada request, a
/// sessão de quem não existe mais (ou virou outra pessoa) morre com 401 — e o
/// front desloga e manda para o login pelo caminho que já existe.
/// </summary>
public static class ValidacaoDeSessao
{
    public static async Task AoTokenValidado(TokenValidatedContext contexto)
    {
        var principal = contexto.Principal;

        if (principal is null)
        {
            contexto.Fail("Sessão inválida. Entre de novo.");
            return;
        }

        if (!principal.ObterId().HasValue)
        {
            contexto.Fail("Sessão inválida. Entre de novo.");
            return;
        }

        var emailDoToken = principal.ObterEmail();

        if (string.IsNullOrWhiteSpace(emailDoToken))
        {
            contexto.Fail("Sessão inválida. Entre de novo.");
            return;
        }

        var banco = contexto.HttpContext.RequestServices.GetRequiredService<LumiDbContext>();

        // Identidade é e-mail ou login, igual ao que o token carrega: entregador
        // pode não ter e-mail.
        var identidadeDoBanco = await banco.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == principal.ObterId()!.Value)
            .Select(u => new { u.Email, u.Login, u.Ativo })
            .SingleOrDefaultAsync();

        // Nulo quando o usuário não existe mais. Diferente quando o id foi
        // reaproveitado por outra pessoa. Inativo não entra. Nos três casos, a
        // sessão morreu.
        if (identidadeDoBanco is null
            || !identidadeDoBanco.Ativo
            || !string.Equals(identidadeDoBanco.Email ?? identidadeDoBanco.Login, emailDoToken, StringComparison.OrdinalIgnoreCase))
        {
            contexto.Fail("Sessão inválida. Entre de novo.");
        }
    }
}
