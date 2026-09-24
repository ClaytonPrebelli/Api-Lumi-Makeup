using Google.Apis.Auth;
using LumiMakeup.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Security;

public class AutenticacaoGoogleServiceTests
{
    private static AutenticacaoGoogleOptions CriarOpcoes(string idCliente = "cliente-do-google-123")
    {
        return new AutenticacaoGoogleOptions { IdCliente = idCliente };
    }

    [Fact]
    public async Task ValidarTokenIdAsync_lanca_quando_id_do_cliente_nao_configurado()
    {
        var servico = new AutenticacaoGoogleService(Options.Create(CriarOpcoes(string.Empty)));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.ValidarTokenIdAsync("token", CancellationToken.None));

        Assert.Equal("IdCliente do Google não configurado (ExternalServices:Google:IdCliente).", excecao.Message);
    }

    [Fact]
    public async Task ValidarTokenIdAsync_retorna_dados_do_usuario_quando_token_valido()
    {
        var servico = new AutenticacaoGoogleService(Options.Create(CriarOpcoes()));
        servico.ValidarEmGoogle = (tokenId, configuracao) =>
        {
            Assert.Equal("token", tokenId);
            Assert.Equal(new[] { "cliente-do-google-123" }, configuracao.Audience);
            return Task.FromResult(new GoogleJsonWebSignature.Payload
            {
                Subject = "google-1",
                Email = "maria@exemplo.com",
                Name = "Maria"
            });
        };

        var dados = await servico.ValidarTokenIdAsync("token", CancellationToken.None);

        Assert.NotNull(dados);
        Assert.Equal("google-1", dados!.IdGoogle);
        Assert.Equal("maria@exemplo.com", dados.Email);
        Assert.Equal("Maria", dados.Nome);
    }

    [Fact]
    public async Task ValidarTokenIdAsync_usa_email_como_nome_quando_nome_ausente()
    {
        var servico = new AutenticacaoGoogleService(Options.Create(CriarOpcoes()));
        servico.ValidarEmGoogle = (_, _) => Task.FromResult(new GoogleJsonWebSignature.Payload
        {
            Subject = "google-2",
            Email = "ana@exemplo.com",
            Name = null
        });

        var dados = await servico.ValidarTokenIdAsync("token", CancellationToken.None);

        Assert.Equal(string.Empty, dados!.Nome);
    }

    [Fact]
    public async Task ValidarTokenIdAsync_retorna_nulo_quando_token_invalido()
    {
        var servico = new AutenticacaoGoogleService(Options.Create(CriarOpcoes()));
        servico.ValidarEmGoogle = (_, _) => throw new InvalidJwtException("assinatura inválida");

        var dados = await servico.ValidarTokenIdAsync("token", CancellationToken.None);

        Assert.Null(dados);
    }
}