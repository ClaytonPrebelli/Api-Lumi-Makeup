using System.Net;
using System.Text;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class ViaCepServiceTests
{
    private static HttpResponseMessage RespostaJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static ViaCepService CriarServico(HttpMessageHandler manipulador)
    {
        return new ViaCepService(Testes.CriarHttpClient(manipulador, "https://viacep.com.br/"), NullLogger<ViaCepService>.Instance);
    }

    [Fact]
    public async Task ConsultarAsync_retorna_endereco_para_cep_encontrado()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""{"cep":"01310-100","logradouro":"Av. Paulista","bairro":"Bela Vista","localidade":"São Paulo","uf":"SP","erro":false}""")));

        var resultado = await servico.ConsultarAsync("01310-100", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal("01310-100", resultado!.Cep);
        Assert.Equal("Av. Paulista", resultado.Logradouro);
        Assert.Equal("Bela Vista", resultado.Bairro);
        Assert.Equal("São Paulo", resultado.Cidade);
        Assert.Equal("SP", resultado.Estado);
    }

    [Fact]
    public async Task ConsultarAsync_retorna_nulo_quando_cep_nao_encontrado()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""{"erro":true}""")));

        var resultado = await servico.ConsultarAsync("00000-000", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_retorna_nulo_quando_resposta_vazia()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("null")));

        var resultado = await servico.ConsultarAsync("01310-100", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_retorna_nulo_quando_ocorrer_falha_de_http()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("", HttpStatusCode.ServiceUnavailable)));

        var resultado = await servico.ConsultarAsync("01310-100", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_retorna_nulo_quando_resposta_invalida()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("não é json")));

        var resultado = await servico.ConsultarAsync("01310-100", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ConsultarAsync_remove_hifens_do_cep_na_consulta()
    {
        string? caminhoConsultado = null;
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            caminhoConsultado = _.RequestUri!.PathAndQuery;
            return RespostaJson("""{"cep":"01310-100","logradouro":"Av. Paulista","bairro":"","localidade":"","uf":"","erro":false}""");
        }));

        await servico.ConsultarAsync("01310-100", CancellationToken.None);

        Assert.Equal("/ws/01310100/json/", caminhoConsultado);
    }
}