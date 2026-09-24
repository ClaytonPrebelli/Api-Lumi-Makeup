using System.Net;
using System.Text.Json;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class RecaptchaValidatorTests
{
    private static RecaptchaOptions CriarOpcoes(string chaveSecreta = "chave", double limite = 0.5)
    {
        return new RecaptchaOptions { ChaveSecreta = chaveSecreta, LimiteDeScore = limite };
    }

    private static RecaptchaValidator CriarValidador(RecaptchaOptions opcoes, HttpMessageHandler? manipulador = null)
    {
        var cliente = manipulador is null
            ? new HttpClient()
            : Testes.CriarHttpClient(manipulador, "https://www.google.com/");

        return new RecaptchaValidator(cliente, Options.Create(opcoes), NullLogger<RecaptchaValidator>.Instance);
    }

    private static HttpResponseMessage RespostaJson(object conteudo, HttpStatusCode status = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(conteudo);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    [Fact]
    public async Task ValidarTokenAsync_aceita_sem_validar_quando_chave_secreta_nao_configurada()
    {
        var validador = CriarValidador(CriarOpcoes(string.Empty));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.True(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_rejeita_quando_token_ausente()
    {
        var validador = CriarValidador(CriarOpcoes());

        var resultado = await validador.ValidarTokenAsync("   ", CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_rejeita_quando_resposta_nao_e_de_sucesso()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = false }, HttpStatusCode.InternalServerError)));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_rejeita_quando_resposta_indica_falha_com_codigos_de_erro()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = false, error_codes = new[] { "invalid-input-response" } })));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_rejeita_quando_resposta_indica_falha_sem_codigos_de_erro()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = false })));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_ao_obter_resposta_invalida_a_validação_lanca_excecao_de_json()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") }));

        await Assert.ThrowsAsync<JsonException>(
            () => validador.ValidarTokenAsync("token", CancellationToken.None));
    }

    [Fact]
    public async Task ValidarTokenAsync_rejeita_quando_score_abaixo_do_limite()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = true, score = 0.2 })));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.False(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_aceita_quando_score_acima_do_limite()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = true, score = 0.9 })));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.True(resultado);
    }

    [Fact]
    public async Task ValidarTokenAsync_aceita_quando_resposta_valida_sem_score()
    {
        var validador = CriarValidador(CriarOpcoes(), new Testes.ManipuladorHttpSimulado(
            RespostaJson(new { success = true })));

        var resultado = await validador.ValidarTokenAsync("token", CancellationToken.None);

        Assert.True(resultado);
    }
}