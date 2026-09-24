using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Security;

public class JwtTokenServiceTests
{
    private const string Segredo = "chave-secreta-super-segura-com-mais-de-32-bytes-0123456789";

    private static TokenJwtOptions CriarOpcoes()
    {
        return new TokenJwtOptions
        {
            Segredo = Segredo,
            Emissor = "lumi-makeup",
            Audiencia = "lumi-makeup-clientes",
            MinutosDeExpiracao = 30,
            DiasDeExpiracaoDoRefresh = 3
        };
    }

    private static JwtTokenService CriarServico(TokenJwtOptions? opcoes = null)
    {
        return new JwtTokenService(Options.Create(opcoes ?? CriarOpcoes()));
    }

    private static Usuario CriarUsuario(string id = "42")
    {
        return new Usuario
        {
            Id = long.Parse(id),
            Nome = "Maria",
            Email = "maria@exemplo.com",
            Papel = PapelUsuario.Cliente
        };
    }

    [Fact]
    public void TokenJwtOptions_define_valores_padrao()
    {
        var opcoes = new TokenJwtOptions();

        Assert.Equal(string.Empty, opcoes.Segredo);
        Assert.Equal(string.Empty, opcoes.Emissor);
        Assert.Equal(string.Empty, opcoes.Audiencia);
        Assert.Equal(60, opcoes.MinutosDeExpiracao);
        Assert.Equal(7, opcoes.DiasDeExpiracaoDoRefresh);
    }

    [Fact]
    public void GerarTokens_produz_token_de_acesso_e_de_refresh()
    {
        var servico = CriarServico();
        var usuario = CriarUsuario();

        var (tokenAcesso, tokenRefresh) = servico.GerarTokens(usuario);

        Assert.False(string.IsNullOrWhiteSpace(tokenAcesso));
        Assert.False(string.IsNullOrWhiteSpace(tokenRefresh));
        Assert.NotEqual(tokenAcesso, tokenRefresh);
    }

    [Fact]
    public void ObterIdDeUsuarioDoTokenRefresh_retorna_id_para_token_de_refresh_valido()
    {
        var servico = CriarServico();
        var usuario = CriarUsuario();
        var (_, tokenRefresh) = servico.GerarTokens(usuario);

        var id = servico.ObterIdDeUsuarioDoTokenRefresh(tokenRefresh);

        Assert.Equal("42", id);
    }

    [Fact]
    public void ObterIdDeUsuarioDoTokenRefresh_retorna_nulo_para_token_de_acesso()
    {
        var servico = CriarServico();
        var (tokenAcesso, _) = servico.GerarTokens(CriarUsuario());

        var id = servico.ObterIdDeUsuarioDoTokenRefresh(tokenAcesso);

        Assert.Null(id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nao-e-um-token")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.abc.def")]
    public void ObterIdDeUsuarioDoTokenRefresh_retorna_nulo_para_token_invalido(string token)
    {
        var servico = CriarServico();

        var id = servico.ObterIdDeUsuarioDoTokenRefresh(token);

        Assert.Null(id);
    }

    [Fact]
    public void ObterIdDeUsuarioDoTokenRefresh_retorna_nulo_para_token_assinado_com_outra_chave()
    {
        var servico = CriarServico();
        var outroServico = CriarServico(new TokenJwtOptions
        {
            Segredo = "outra-chave-secreta-diferente-com-tamanho-suficiente-para-hmac-aaaa",
            Emissor = "lumi-makeup",
            Audiencia = "lumi-makeup-clientes"
        });
        var (_, tokenRefresh) = outroServico.GerarTokens(CriarUsuario());

        var id = servico.ObterIdDeUsuarioDoTokenRefresh(tokenRefresh);

        Assert.Null(id);
    }
}