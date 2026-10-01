using System.Net;
using System.Text;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class GeocodificadorEmCadeiaTests
{
    private static HttpResponseMessage Resposta(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static GeocodificadorEmCadeia Servico(Func<HttpRequestMessage, HttpResponseMessage> fabrica) =>
        new(
            Testes.CriarHttpClient(new Testes.ManipuladorHttpSimulado(fabrica), "https://photon.komoot.io/"),
            NullLogger<GeocodificadorEmCadeia>.Instance);

    // GeoJSON vem como [longitude, latitude]. Invertido, o ponto cai no
    // hemisferio errado e a distancia de Sorocaba sai absurda.
    [Fact]
    public async Task GeocodificarAsync_lê_a_coordenada_na_ordem_do_geojson()
    {
        var servico = Servico(_ => Resposta("""
        {"type":"FeatureCollection","features":[{"type":"Feature",
          "properties":{"osm_type":"","osm_value":"postcode","postcode":"18072-759"},
          "geometry":{"type":"Point","coordinates":[-47.5098902,-23.4445647]}}]}
        """));

        var resultado = await servico.GeocodificarAsync("18072-759", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.4445647m, resultado!.Value.Latitude);
        Assert.Equal(-47.5098902m, resultado.Value.Longitude);
    }

    [Fact]
    public async Task GeocodificarAsync_aceita_cep_de_sete_digitos()
    {
        string? consulta = null;
        var servico = Servico(_ =>
        {
            consulta = _.RequestUri!.PathAndQuery;
            return Resposta("""
            {"features":[{"properties":{"osm_value":"postcode"},
              "geometry":{"type":"Point","coordinates":[-47.5098902,-23.4445647]}}]}
            """);
        });

        // A tela escreve 18072-856, que são sete dígitos. Sem completar com
        // zero, o CEP era descartado e a busca saía com o endereço inteiro.
        var resultado = await servico.GeocodificarAsync("18072-856", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Contains("18072-856", consulta!);
    }

    [Fact]
    public async Task GeocodificarAsync_consulta_pelo_cep_e_nao_pela_cidade()
    {
        var consulta = new List<string>();
        var servico = Servico(_ =>
        {
            consulta.Add(_.RequestUri!.PathAndQuery);
            return Resposta("""
            {"features":[{"properties":{},"geometry":{"type":"Point","coordinates":[-47.5,-23.44]}}]}
            """);
        });

        await servico.GeocodificarAsync("18072-759, Rua X, Jardim Y, Sorocaba, SP", CancellationToken.None);

        // Busca por cidade devolve o centro, e dois CEPs da mesma cidade cairiam
        // no mesmo ponto - a distância sairia zero.
        Assert.Contains("postalcode", consulta[0]);
        Assert.DoesNotContain("Sorocaba", consulta[0]);
    }

    [Fact]
    public async Task GeocodificarAsync_nao_manda_o_parametro_de_idioma()
    {
        var consulta = string.Empty;
        var servico = Servico(_ =>
        {
            consulta = _.RequestUri!.PathAndQuery;
            return Resposta("""
            {"features":[{"properties":{},"geometry":{"type":"Point","coordinates":[-47.5,-23.44]}}]}
            """);
        });

        await servico.GeocodificarAsync("18072-759", CancellationToken.None);

        // O Photon responde 400 para lang=pt, embora aceite lang=de. Mandar um
        // idioma fora da lista dele derruba a consulta inteira, e o 400 se
        // confunde com "não achou o endereço".
        Assert.DoesNotContain("lang=", consulta);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_nulo_quando_o_servico_erra()
    {
        var servico = Servico(_ => Resposta("erro", HttpStatusCode.BadRequest));

        var resultado = await servico.GeocodificarAsync("18072-759", CancellationToken.None);

        Assert.Null(resultado);
        Assert.NotNull(servico.UltimaFalha);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_nulo_quando_nao_ha_resultado()
    {
        var servico = Servico(_ => Resposta("""{"features":[]}"""));

        var resultado = await servico.GeocodificarAsync("18072-759", CancellationToken.None);

        // Sem coordenada, o cálculo recusa. Virar para o centro da cidade daria
        // um número plausível e errado, e o cliente pagaria por um lugar em que
        // ele não está.
        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_nulo_sem_cep()
    {
        var servico = Servico(_ => Resposta("""{"features":[]}"""));

        Assert.Null(await servico.GeocodificarAsync("Rua sem numero", CancellationToken.None));
    }

    /// <summary>
    /// Prova que a falha do BaseAddress aparece como erro de requisição, e não
    /// como "não achou o endereço".
    ///
    /// São as duas mensagens que a tela mostra, e confundi-las custou um deploy
    /// inteiro: o cálculo dizia que o mapa não tinha o CEP quando o problema
    /// era a URL montada.
    /// </summary>
    [Fact]
    public async Task GeocodificarAsync_sem_baseaddress_falha_com_erro_de_requisicao()
    {
        using var cliente = new HttpClient(
            new Testes.ManipuladorHttpSimulado(_ => Resposta("""{"features":[]}""")));

        var servico = new GeocodificadorEmCadeia(cliente, NullLogger<GeocodificadorEmCadeia>.Instance);

        // Sem BaseAddress a requisição nem sai, e o erro não é de rede nem de mapa.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.GeocodificarAsync("18072-759", CancellationToken.None));
    }

    /// <summary>
    /// Trava o que a tela de produção usa: a cadeia montada pelo container, com
    /// o BaseAddress do Photon.
    ///
    /// O cliente é criado à mão, e não pelo helper de teste, porque o helper já
    /// define BaseAddress - e isso esconderia justamente o defeito.
    /// </summary>
    [Fact]
    public async Task GeocodificarAsync_aceita_o_registro_de_producao()
    {
        using var cliente = new HttpClient(
            new Testes.ManipuladorHttpSimulado(_ => Resposta("""
            {"features":[{"properties":{"osm_value":"postcode"},
              "geometry":{"type":"Point","coordinates":[-47.5098902,-23.4445647]}}]}
            """)))
        {
            BaseAddress = new Uri("https://photon.komoot.io/"),
            Timeout = TimeSpan.FromSeconds(20)
        };

        var servico = new GeocodificadorEmCadeia(cliente, NullLogger<GeocodificadorEmCadeia>.Instance);

        var resultado = await servico.GeocodificarAsync("18072-759", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.4445647m, resultado!.Value.Latitude);
    }
}
