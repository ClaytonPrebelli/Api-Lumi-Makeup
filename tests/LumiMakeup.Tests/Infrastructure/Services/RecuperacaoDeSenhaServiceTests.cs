using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class RecuperacaoDeSenhaServiceTests
{
    private static readonly FrontendOptions OpcoesDoFrontend = new()
    {
        UrlBase = "https://lumimakeup.com.br",
        RotaDeRedefinicaoDeSenha = "/redefinir-senha",
        MinutosDeExpiracaoDoTokenDeReset = 30
    };

    private static string CalcularHashDoToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task<Usuario> CriarUsuarioComSenhaAsync(LumiDbContext contexto)
    {
        var hasher = new PasswordHasher<Usuario>();
        var usuario = new Usuario
        {
            Nome = "Julia Udinal Americo",
            Email = "use.lumimakeup@gmail.com",
            Papel = PapelUsuario.Cliente
        };
        usuario.HashSenha = hasher.HashPassword(usuario, "senhaAntiga123");

        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync();

        return usuario;
    }

    private static async Task<(
        RecuperacaoDeSenhaService Servico,
        Mock<IEmailSender> Email,
        Mock<ITokenService> Token,
        LumiDbContext Contexto)> CriarServicoAsync(bool comUsuarioComSenha = true)
    {
        var contexto = Testes.CriarContextoInMemory();
        if (comUsuarioComSenha)
        {
            await CriarUsuarioComSenhaAsync(contexto);
        }

        var email = new Mock<IEmailSender>();
        var token = new Mock<ITokenService>();
        token.Setup(t => t.GerarTokens(It.IsAny<Usuario>()))
            .Returns(("acesso", "refresh"));

        var servico = new RecuperacaoDeSenhaService(
            contexto,
            new PasswordHasher<Usuario>(),
            token.Object,
            email.Object,
            Options.Create(OpcoesDoFrontend));

        return (servico, email, token, contexto);
    }

    [Fact]
    public async Task Solicitar_cria_recuperacao_e_envia_email_com_link_de_redefinicao()
    {
        var (servico, email, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        string? destinatario = null;
        string? assunto = null;
        string? corpo = null;
        email.Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((destino, tema, html, _) =>
            {
                destinatario = destino;
                assunto = tema;
                corpo = html;
            })
            .Returns(Task.CompletedTask);

        await servico.SolicitarAsync(new RequisicaoDeSolicitarResetDeSenha(usuario.Email), CancellationToken.None);

        email.Verify(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(usuario.Email, destinatario);
        Assert.Contains("Redefinição de senha", assunto);
        Assert.Contains("LUMI&nbsp;MAKEUP", corpo);
        Assert.Contains("beleza que ilumina", corpo);
        Assert.Contains("Julia Udinal Americo", corpo);
        Assert.Contains("https://lumimakeup.com.br/redefinir-senha?token=", corpo);
        Assert.Contains("Redefinir minha senha", corpo);
        Assert.Contains("30 minutos", corpo);
        Assert.Contains("nao-responda@lumimakeup.com.br", corpo);

        var correspondencia = Regex.Match(corpo!, "token=([A-Za-z0-9_-]+)");
        Assert.True(correspondencia.Success);
        var token = correspondencia.Groups[1].Value;
        Assert.Equal(43, token.Length);

        var recuperacao = await contexto.RecuperacoesDeSenha.SingleAsync();
        Assert.Equal(CalcularHashDoToken(token), recuperacao.HashToken);
        Assert.Equal(usuario.Id, recuperacao.UsuarioId);
        Assert.True(recuperacao.ExpiracaoEm >= DateTime.UtcNow.AddMinutes(29));
        Assert.True(recuperacao.ExpiracaoEm <= DateTime.UtcNow.AddMinutes(31));
        Assert.Null(recuperacao.UtilizadoEm);
    }

    [Fact]
    public async Task Solicitar_nao_envia_email_quando_usuario_nao_existe()
    {
        var (servico, email, _, contexto) = await CriarServicoAsync(comUsuarioComSenha: false);

        await servico.SolicitarAsync(new RequisicaoDeSolicitarResetDeSenha("nao-existe@exemplo.com"), CancellationToken.None);

        email.Verify(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(contexto.RecuperacoesDeSenha);
    }

    [Fact]
    public async Task Solicitar_nao_envia_email_para_usuario_sem_senha()
    {
        var contexto = Testes.CriarContextoInMemory();
        contexto.Usuarios.Add(new Usuario
        {
            Nome = "Maria",
            Email = "maria@exemplo.com",
            IdGoogle = "google-123",
            Papel = PapelUsuario.Cliente
        });
        await contexto.SaveChangesAsync();

        var email = new Mock<IEmailSender>();
        var servico = new RecuperacaoDeSenhaService(
            contexto,
            new PasswordHasher<Usuario>(),
            new Mock<ITokenService>().Object,
            email.Object,
            Options.Create(OpcoesDoFrontend));

        await servico.SolicitarAsync(new RequisicaoDeSolicitarResetDeSenha("maria@exemplo.com"), CancellationToken.None);

        email.Verify(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(contexto.RecuperacoesDeSenha);
    }

    [Fact]
    public async Task Solicitar_revoga_recuperacoes_anteriores_do_mesmo_usuario()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        contexto.RecuperacoesDeSenha.AddRange(
            new RecuperacaoDeSenha { UsuarioId = usuario.Id, HashToken = "ANTIGO1", ExpiracaoEm = DateTime.UtcNow.AddMinutes(30) },
            new RecuperacaoDeSenha { UsuarioId = usuario.Id, HashToken = "ANTIGO2", ExpiracaoEm = DateTime.UtcNow.AddMinutes(30) });
        await contexto.SaveChangesAsync();

        await servico.SolicitarAsync(new RequisicaoDeSolicitarResetDeSenha(usuario.Email), CancellationToken.None);

        var recuperacoes = await contexto.RecuperacoesDeSenha.ToListAsync();
        var recuperacao = Assert.Single(recuperacoes);
        Assert.NotEqual("ANTIGO1", recuperacao.HashToken);
        Assert.NotEqual("ANTIGO2", recuperacao.HashToken);
    }

    [Fact]
    public async Task Confirmar_redefine_a_senha_e_retorna_nova_resposta_de_autenticacao()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        var token = "token-de-reset-valido";
        contexto.RecuperacoesDeSenha.Add(new RecuperacaoDeSenha
        {
            UsuarioId = usuario.Id,
            HashToken = CalcularHashDoToken(token),
            CriadoEm = DateTime.UtcNow,
            ExpiracaoEm = DateTime.UtcNow.AddMinutes(30)
        });
        await contexto.SaveChangesAsync();

        var resposta = await servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha(token, "novaSenha123"), CancellationToken.None);

        Assert.Equal("acesso", resposta.TokenAcesso);
        Assert.Equal("refresh", resposta.TokenRefresh);
        Assert.Equal(usuario.Id, resposta.Usuario.Id);

        var hasher = new PasswordHasher<Usuario>();
        var usuarioAtualizado = await contexto.Usuarios.SingleAsync();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(usuarioAtualizado, usuarioAtualizado.HashSenha!, "novaSenha123"));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(usuarioAtualizado, usuarioAtualizado.HashSenha!, "senhaAntiga123"));

        var recuperacao = await contexto.RecuperacoesDeSenha.SingleAsync();
        Assert.NotNull(recuperacao.UtilizadoEm);
    }

    [Fact]
    public async Task Confirmar_remove_as_outras_recuperacoes_do_usuario()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        var tokenUsado = "token-usado";
        contexto.RecuperacoesDeSenha.AddRange(
            new RecuperacaoDeSenha { UsuarioId = usuario.Id, HashToken = CalcularHashDoToken(tokenUsado), CriadoEm = DateTime.UtcNow, ExpiracaoEm = DateTime.UtcNow.AddMinutes(30) },
            new RecuperacaoDeSenha { UsuarioId = usuario.Id, HashToken = "OUTRO-TOKEN", CriadoEm = DateTime.UtcNow, ExpiracaoEm = DateTime.UtcNow.AddMinutes(30) });
        await contexto.SaveChangesAsync();

        await servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha(tokenUsado, "novaSenha123"), CancellationToken.None);

        var recuperacoes = await contexto.RecuperacoesDeSenha.ToListAsync();
        var recuperacao = Assert.Single(recuperacoes);
        Assert.Equal(CalcularHashDoToken(tokenUsado), recuperacao.HashToken);
    }

    [Fact]
    public async Task Confirmar_lanca_quando_token_nao_encontrado()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha("token-desconhecido", "novaSenha123"), CancellationToken.None));

        Assert.Equal("Token de recuperação inválido ou expirado.", excecao.Message);
    }

    [Fact]
    public async Task Confirmar_lanca_quando_token_ja_expirado()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        contexto.RecuperacoesDeSenha.Add(new RecuperacaoDeSenha
        {
            UsuarioId = usuario.Id,
            HashToken = CalcularHashDoToken("token-expirado"),
            CriadoEm = DateTime.UtcNow.AddMinutes(-60),
            ExpiracaoEm = DateTime.UtcNow.AddMinutes(-30)
        });
        await contexto.SaveChangesAsync();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha("token-expirado", "novaSenha123"), CancellationToken.None));

        Assert.Equal("Token de recuperação inválido ou expirado.", excecao.Message);
    }

    [Fact]
    public async Task Confirmar_lanca_quando_token_ja_utilizado()
    {
        var (servico, _, _, contexto) = await CriarServicoAsync();
        var usuario = await contexto.Usuarios.SingleAsync();
        contexto.RecuperacoesDeSenha.Add(new RecuperacaoDeSenha
        {
            UsuarioId = usuario.Id,
            HashToken = CalcularHashDoToken("token-usado"),
            CriadoEm = DateTime.UtcNow.AddMinutes(-10),
            ExpiracaoEm = DateTime.UtcNow.AddMinutes(20),
            UtilizadoEm = DateTime.UtcNow.AddMinutes(-5)
        });
        await contexto.SaveChangesAsync();

        var excecao = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha("token-usado", "novaSenha123"), CancellationToken.None));

        Assert.Equal("Token de recuperação inválido ou expirado.", excecao.Message);
    }

    [Fact]
    public async Task Confirmar_lanca_quando_senha_muito_curta()
    {
        var (servico, _, _, _) = await CriarServicoAsync();

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.ConfirmarAsync(new RequisicaoDeConfirmarResetDeSenha("token", "123"), CancellationToken.None));

        Assert.Equal("A senha deve ter no mínimo 6 caracteres.", excecao.Message);
    }
}