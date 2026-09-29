using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class RepositorioDeSessaoWhatsAppTests
{
    private static RepositorioDeSessaoWhatsApp CriarComContexto(LumiDbContext contexto) =>
        new(contexto, NullLogger<RepositorioDeSessaoWhatsApp>.Instance);

    [Fact]
    public async Task devolve_null_quando_o_numero_ainda_nao_foi_pareado()
    {
        await using var contexto = Testes.CriarContextoInMemory();

        var sessao = await CriarComContexto(contexto).ObterAsync();

        // Null e o que diz ao Node que ele precisa gerar QR. Um objeto vazio
        // faria o Node tentar conectar com credenciais que nao existem.
        Assert.Null(sessao);
    }

    [Fact]
    public async Task guarda_e_devolve_a_sessao_gravada()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var repositorio = CriarComContexto(contexto);

        var gravado = await repositorio.GravarAsync("{\"noiseKey\":\"abc\"}", "{\"pre-key:1\":{}}", 0);

        Assert.True(gravado);

        var sessao = await repositorio.ObterAsync();

        Assert.NotNull(sessao);
        Assert.Equal("{\"noiseKey\":\"abc\"}", sessao.Credenciais);
        Assert.Equal("{\"pre-key:1\":{}}", sessao.Chaves);
    }

    [Fact]
    public async Task a_versao_sobe_a_cada_gravacao()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var repositorio = CriarComContexto(contexto);

        await repositorio.GravarAsync("{\"v\":1}", "{}", 0);
        var depoisDaPrimeira = await repositorio.ObterAsync();

        await repositorio.GravarAsync("{\"v\":2}", "{}", depoisDaPrimeira!.Versao);
        var depoisDaSegunda = await repositorio.ObterAsync();

        // A versao e o que impede que o contêiner antigo sobrescreva a sessao
        // do novo durante um redesplie.
        Assert.Equal(1, depoisDaPrimeira!.Versao);
        Assert.Equal(2, depoisDaSegunda!.Versao);
    }

    [Fact]
    public async Task recusa_gravacao_quando_a_versao_nao_confere()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var repositorio = CriarComContexto(contexto);

        await repositorio.GravarAsync("{\"original\":true}", "{}", 0);

        // O Node mandou a versao 0 de novo, mas o banco ja esta na 1: outro
        // contêiner escreveu depois que ele leu.
        var gravado = await repositorio.GravarAsync("{\"conflitante\":true}", "{}", 0);

        Assert.False(gravado);

        // E o detalhe que importa: o banco continua com a sessao boa. Recusar
        // sem gravar e o que evita perder o pareamento.
        var sessao = await repositorio.ObterAsync();

        Assert.Equal("{\"original\":true}", sessao!.Credenciais);
        Assert.Equal(1, sessao.Versao);
    }

    [Fact]
    public async Task apaga_a_sessao_quando_o_whatsapp_desconecta_o_numero()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var repositorio = CriarComContexto(contexto);

        await repositorio.GravarAsync("{\"v\":1}", "{}", 0);
        await repositorio.ApagarAsync();

        // Sem apagar, o Node ficaria tentando reconectar com credenciais que a
        // Meta invalidou, e nem QR nem envio voltariam.
        Assert.Null(await repositorio.ObterAsync());
    }

    [Fact]
    public async Task guardar_depois_de_apagar_cria_a_sessao_de_novo()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var repositorio = CriarComContexto(contexto);

        await repositorio.GravarAsync("{\"v\":1}", "{}", 0);
        await repositorio.ApagarAsync();

        // O Node que apagou volta a gravrar com a versao que tinha em maos, e
        // nao com a versao 0. Se a gravacao exigisse zero, o segundo pareamento
        // seria recusado.
        var gravado = await repositorio.GravarAsync("{\"novo\":true}", "{}", 1);

        Assert.True(gravado);

        var sessao = await repositorio.ObterAsync();

        Assert.Equal("{\"novo\":true}", sessao!.Credenciais);
        Assert.Equal(1, sessao.Versao);
    }
}
