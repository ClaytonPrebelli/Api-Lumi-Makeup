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

        var resultado = await servico.GeocodificarAsync("01425-001, Av. Paulista, Bela Vista, São Paulo, SP", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.550520m, resultado!.Value.Latitude);
        Assert.Equal(-46.633309m, resultado.Value.Longitude);
    }

    // Endereco de verdade, com o CEP que a consulta usa. A cidade nao e
    // necessaria: quem devolve o ponto e' o CEP, e a cidade so serviria para
    // trazer o centro da cidade, que nao e o endereco do cliente.
    private const string EnderecoValido = "01425-001, Av. Paulista, Bela Vista, São Paulo, SP";

    [Fact]
    public async Task GeocodificarAsync_aceita_o_cep_sozinho_sem_cidade()
    {
        // A consulta e' pelo CEP, e o CEP sozinho e' o suficiente. Exigir cidade
        // descartava as consultas que trazem so o CEP - que sao as que o
        // calculo e o diagnostico montam - e o resultado era "sem coordenada"
        // sem nunca chegar a falar com o mapa.
        var url = string.Empty;
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            url = _.RequestUri!.PathAndQuery;
            return RespostaJson("""[{"lat":"-23.4538389","lon":"-47.4969911","type":"postcode"}]""");
        }));

        var resultado = await servico.GeocodificarAsync("18072-856", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.4538389m, resultado!.Value.Latitude);
        Assert.Contains("postalcode=18072-856", url);
    }

    [Fact]
    public async Task GeocodificarAsync_explica_por_que_nao_encontrou()
    {
        // O calculo trata "sem ponto" e "falha de rede" do mesmo jeito, e quem
        // diagnostica precisa saber qual foi: um CEP sem ponto se resolve no
        // mapa, uma falha de rede se resolve no servidor.
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ => RespostaJson("[]")));

        await servico.GeocodificarAsync("18072-856", CancellationToken.None);

        Assert.NotNull(servico.UltimoFalha);
        Assert.Contains("18072-856", servico.UltimoFalha!);
    }

    [Fact]
    public async Task GeocodificarAsync_explica_a_falha_de_http()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
            RespostaJson("blocked", HttpStatusCode.Forbidden)));

        await servico.GeocodificarAsync("18072-856", CancellationToken.None);

        // 403 e bloqueio por User-Agent, 429 e excesso de requisicao: causas
        // diferentes, e o diagnostico precisa distinguir.
        Assert.NotNull(servico.UltimoFalha);
        Assert.Contains("403", servico.UltimoFalha!);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_lista_vazia()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("[]")));

        var resultado = await servico.GeocodificarAsync(EnderecoValido, CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_resposta_null()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("null")));

        var resultado = await servico.GeocodificarAsync(EnderecoValido, CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_ocorrer_falha_de_http()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("", HttpStatusCode.TooManyRequests)));

        var resultado = await servico.GeocodificarAsync(EnderecoValido, CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_retorna_nulo_quando_resposta_invalida()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(RespostaJson("não é json")));

        var resultado = await servico.GeocodificarAsync(EnderecoValido, CancellationToken.None);

        // Json malformado tem de virar null, e nao excecao: a tela do frete
        // mostra "tente de novo", e um 500 aqui seria erro de servidor sem
        // causa visivel.
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

        await servico.GeocodificarAsync("01425-001, Av. Paulista, Bela Vista, São Paulo, SP", CancellationToken.None);

        Assert.NotNull(caminhoEQuery);
        Assert.StartsWith("/search?", caminhoEQuery);
        Assert.Contains("format=json", caminhoEQuery);
        Assert.Contains("limit=1", caminhoEQuery);
    }

    [Fact]
    public async Task GeocodificarAsync_usa_a_consulta_estruturada()
    {
        string? caminhoEQuery = null;
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            caminhoEQuery = _.RequestUri!.PathAndQuery;
            return RespostaJson("""[{"lat":"0","lon":"0"}]""");
        }));

        // Este e o formato que a tela de frete monta: CEP entre aspas, com
        // logradouro, bairro, cidade e estado.
        await servico.GeocodificarAsync("\"01000-000\", Av. Paulista, Bela Vista, São Paulo, SP", CancellationToken.None);

        Assert.NotNull(caminhoEQuery);

        // A busca por texto livre era a causa da falha na tela: aspas no CEP e a
        // sigla do estado faziam o Nominatim responder 200 com lista vazia, e
        // o salvamento falhava sem nenhuma mensagem de erro.
        Assert.Contains("postalcode=01000-000", caminhoEQuery);
        Assert.DoesNotContain("q=", caminhoEQuery);
    }

    [Fact]
    public async Task GeocodificarAsync_usa_o_ponto_do_cep_e_nao_o_centro_da_cidade()
    {
        // Somando a cidade a consulta, o Nominatim devolve o CENTRO da cidade, do
        // tipo "administrative". Dois CEPs da mesma cidade caem no mesmo ponto, a
        // distancia sai zero, e todo cliente da mesma cidade paga a taxa minima
        // como se morasse ao lado.
        //
        // Em Sorocaba, 18072-759 e 18080-001 ficam a quase 7 km, uma ponta e
        // outra. Com o centro da cidade, a distancia seria zero e o frete errado
        // para o resto da cidade.
        var urls = new List<string>();

        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            urls.Add(_.RequestUri!.PathAndQuery);
            return RespostaJson("""[{"lat":"-23.4870228","lon":"-47.4644000","type":"postcode"}]""");
        }));

        await servico.GeocodificarAsync("18080-001, Rua Comendador Hermelino Matarazzo, Vila Santa Rita, Sorocaba, SP", CancellationToken.None);

        Assert.NotEmpty(urls);

        // A primeira consulta e so pelo CEP: e ela que traz o ponto do bairro.
        // Uma unica chamada, porque o ponto do CEP ja responde.
        Assert.Equal(1, urls.Count);
        Assert.Contains("postalcode=18080-001", urls[0]);
        Assert.DoesNotContain("city=", urls[0]);
        Assert.DoesNotContain("country=", urls[0]);
    }

    [Fact]
    public async Task GeocodificarAsync_nao_cai_no_centro_da_cidade()
    {
        // CEP sem ponto no OpenStreetMap volta vazio, e a resposta honesta e
        // dizer que nao achou.
        //
        // O plano B com a cidade devolveria o centroide, que fica a kilometros do
        // endereco real. O frete sairia com um numero plausivel e errado, e o
        // cliente pagaria por um lugar em que ele nao esta.
        var chamadas = 0;

        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            chamadas++;

            return chamadas == 1
                ? RespostaJson("[]")
                : RespostaJson("""[{"lat":"-23.5003451","lon":"-47.4582864","type":"administrative"}]""");
        }));

        var resultado = await servico.GeocodificarAsync("01000-000, Sé, São Paulo, SP", CancellationToken.None);

        Assert.Null(resultado);

        // Uma consulta so: nao ha segunda tentativa com a cidade.
        Assert.Equal(1, chamadas);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_o_ponto_real_do_cep_de_sorocaba()
    {
        // Endereco exato que a tela de frete monta para a loja de Sorocaba.
        // As coordenadas abaixo vieram do Nominatim de verdade para o CEP, e
        // sao diferentes do centro da cidade - que e exatamente o que o teste
        // precisa travar.
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""[{"lat":"-23.4445647","lon":"-47.5098902","type":"postcode"}]""")));

        var resultado = await servico.GeocodificarAsync("\"18072-759\", Rua Rosalina Ribeiro, Jardim Golden Park Residence II, Sorocaba, SP", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal(-23.4445647m, resultado!.Value.Latitude);
        Assert.Equal(-47.5098902m, resultado.Value.Longitude);
    }

    [Fact]
    public async Task GeocodificarAsync_monta_a_url_que_o_nominatim_aceita()
    {
        // Este e o endereco exato que a tela de frete monta, e a URL que o
        // parser precisa gerar. Conferida contra o Nominatim de verdade: e ela
        // que devolve as coordenadas do bairro, e nao o centro da cidade.
        var urls = new List<string>();
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            urls.Add(_.RequestUri!.PathAndQuery);
            return RespostaJson("""[{"lat":"-23.5506507","lon":"-46.6333824"}]""");
        }));

        var resultado = await servico.GeocodificarAsync("\"15000-000\", Alameda Santos, Bela Vista, São Paulo, SP", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.NotEmpty(urls);

        var url = System.Web.HttpUtility.UrlDecode(urls[0]);

        // O CEP vem sozinho, sem cidade. E o que garante o ponto do bairro: com
        // a cidade, o Nominatim responde o centro.
        Assert.Contains("postalcode=15000-000", url);
        Assert.DoesNotContain("city=", url);
    }

    [Fact]
    public async Task GeocodificarAsync_aceita_o_cep_entre_aspas()
    {
        // O formato antigo trazia o CEP entre aspas, e a tela ainda manda
        // assim em alguns caminhos. As aspas nao podem impedir a consulta.
        //
        // Este teste antes exigia o contrario - que o CEP sozinho devolvesse
        // nulo - com o argumento de que o mesmo CEP atende varias cidades. O
        // argumento nao se sustenta: CEP brasileiro tem oito digitos e
        // identifica um unico lugar, e a consulta ja vai so por ele.
        var url = string.Empty;
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(_ =>
        {
            url = _.RequestUri!.PathAndQuery;
            return RespostaJson("""[{"lat":"-23.550520","lon":"-46.633309"}]""");
        }));

        var resultado = await servico.GeocodificarAsync("\"01000-000\"", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Contains("postalcode=01000-000", url);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_nulo_quando_a_resposta_tem_texto_no_lugar_de_numero()
    {
        // Um Parse sem cultura aqui viraria FormatException e a tela mostraria
        // 500 em vez da mensagem util.
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""[{"lat":"","lon":""}]""")));

        var resultado = await servico.GeocodificarAsync("Av. Paulista, São Paulo, SP", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task GeocodificarAsync_devolve_nulo_quando_o_endereco_nao_tem_cidade()
    {
        var servico = CriarServico(new Testes.ManipuladorHttpSimulado(
            RespostaJson("""[{"lat":"-23.550520","lon":"-46.633309"}]""")));

        var resultado = await servico.GeocodificarAsync("Av. Paulista, 1578", CancellationToken.None);

        Assert.Null(resultado);
    }
}