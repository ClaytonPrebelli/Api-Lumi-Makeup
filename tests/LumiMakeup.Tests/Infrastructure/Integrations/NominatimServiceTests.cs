using System.Net;
using System.Text;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class NominatimServiceTests
{
    private static HttpResponseMessage RespostaJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static NominatimService CriarServico(HttpMessageHandler manipulador)
    {
        return new NominatimService(Testes.CriarHttpClient(manipulador, "https://nominatim.openstreetmap.org/"), NullLogger<NominatimService>.Instance);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_coordenadas_para_endereco_encontrado()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""[{"lat":"-23.550520","lon":"-46.633309"}]""")));

        var resultado = await servico.GeocodificarAsync("Av. Paulista, 1000, Bela Vista, São Paulo - SP", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.550520m, resultado!.Value.Latitude);
        Assert.Equal(-46.633309m, resultado.Value.Longitude);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_lista_vazia()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("[]")));

        var resultado = await servico.GeocodificarAsync("endereço", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_resposta_null()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("null")));

        var resultado = await servico.GeocodificarAsync("endereço", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_ocorrer_falha_de_http()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("", HttpStatusCode.TooManyRequests)));

        var resultado = await servico.GeocodificarAsync("endereço", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_resposta_invalida()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("não é json")));

        var resultado = await servico.GeocodificarAsync("endereço", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_codifica_o_endereco_na_consulta()
    {
        string? caminhoEQuery = null;
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            caminhoEQuery = _.RequestUri!.PathAndQuery;
            return RespostaJson("""[{"lat":"0","lon":"0"}]""");
        }));

        await servico.GeocodificarAsync("Av. Paulista, 1000", CancellationToken.None);

        Assert.NotNull(caminhoEQuery);
        Assert.StartsWith("/search?", caminhoEQuery);
        Assert.Contains("q=Av.+Paulista%2c+1000", caminhoEQuery);
        Assert.Contains("format=json", caminhoEQuery);
        Assert.Contains("limit=1", caminhoEQuery);
    }
}