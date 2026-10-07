using System.Security.Claims;
using LumiMakeup.Api.Extensions;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LumiMakeup.Tests.Api;

/// <summary>
/// A sessão é id + e-mail do banco, e não só a assinatura.
///
/// Depois de um reset, o id do token pode ser de outra pessoa. Sem a
/// conferência, um pedido feito com a sessão antiga cairia na conta errada.
/// </summary>
public class SessaoTests
{
    private static (DefaultHttpContext Http, LumiDbContext Banco) ContextoComBanco(string nomeDoBanco)
    {
        var servicos = new ServiceCollection();
        servicos.AddDbContext<LumiDbContext>(opcoes => opcoes.UseInMemoryDatabase(nomeDoBanco));
        var provedor = servicos.BuildServiceProvider();

        var http = new DefaultHttpContext { RequestServices = provedor };
        var banco = provedor.GetRequiredService<LumiDbContext>();
        banco.Database.EnsureCreated();

        return (http, banco);
    }

    private static TokenValidatedContext ContextoDeToken(
        DefaultHttpContext http,
        string sub,
        string? email)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, sub) };

        if (email is not null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // O construtor não copia http.User: no pipeline é o handler que
        // preenche Principal. Aqui fazemos o papel dele.
        var contexto = new TokenValidatedContext(
            http,
            new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = principal
        };

        return contexto;
    }

    [Fact]
    public async Task Sessao_valida_passa()
    {
        var (http, banco) = ContextoComBanco("sessao-ok-" + Guid.NewGuid());
        var ana = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", Papel = PapelUsuario.Cliente };
        banco.Usuarios.Add(ana);
        await banco.SaveChangesAsync();

        Assert.NotEqual(0, ana.Id);

        var contexto = ContextoDeToken(http, ana.Id.ToString(), "ana@exemplo.com");
        await ValidacaoDeSessao.AoTokenValidado(contexto);

        Assert.Null(contexto.Result);
    }

    [Fact]
    public async Task Sessao_de_usuario_que_nao_existe_mais_cai()
    {
        // Simula o reset: a conta sumiu, mas o token continua assinado.
        var (http, _) = ContextoComBanco("sessao-sem-usuario-" + Guid.NewGuid());

        var contexto = ContextoDeToken(http, "1", "ana@exemplo.com");
        await ValidacaoDeSessao.AoTokenValidado(contexto);

        Assert.NotNull(contexto.Result?.Failure);
    }

    [Fact]
    public async Task Sessao_de_id_reaproveitado_por_outra_pessoa_cai()
    {
        // Simula o reset com recadastro: o MESMO id agora é de outra pessoa.
        // Sem conferir o e-mail, o pedido cairia na conta dela.
        var (http, banco) = ContextoComBanco("sessao-trocada-" + Guid.NewGuid());
        banco.Usuarios.Add(new Usuario { Id = 1, Nome = "Bia", Email = "bia@exemplo.com", Papel = PapelUsuario.Cliente });
        await banco.SaveChangesAsync();

        var contexto = ContextoDeToken(http, "1", "ana@exemplo.com");
        await ValidacaoDeSessao.AoTokenValidado(contexto);

        Assert.NotNull(contexto.Result?.Failure);
    }

    [Fact]
    public async Task Sessao_sem_email_no_token_cai()
    {
        var (http, banco) = ContextoComBanco("sessao-sem-email-" + Guid.NewGuid());
        var ana = new Usuario { Nome = "Ana", Email = "ana@exemplo.com", Papel = PapelUsuario.Cliente };
        banco.Usuarios.Add(ana);
        await banco.SaveChangesAsync();

        var contexto = ContextoDeToken(http, ana.Id.ToString(), null);
        await ValidacaoDeSessao.AoTokenValidado(contexto);

        Assert.NotNull(contexto.Result?.Failure);
    }
}
