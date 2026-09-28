using System.Net;
using System.Text.Json;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class MelhoradorDeTextoOpenAiCompativelTests
{
    private const string DescricaoValida = "Batom matte de longa duracao, cor vinho";

    private static OpcoesDeIa CriarOpcoes(
        string chave = "chave-de-teste",
        string urlBase = "https://api.groq.com/openai/v1",
        string modelo = "openai/gpt-oss-120b")
    {
        return new OpcoesDeIa { Chave = chave, UrlBase = urlBase, Modelo = modelo };
    }

    private sealed class RequisicaoCapturada
    {
        public HttpMethod? Metodo { get; set; }
        public Uri? Url { get; set; }
        public string Corpo { get; set; } = string.Empty;
        public string? EsquemaDoToken { get; set; }
        public string? Token { get; set; }
    }

    private sealed class Capturador : HttpMessageHandler
    {
        private readonly HttpResponseMessage _resposta;
        private readonly RequisicaoCapturada _destino;

        public Capturador(HttpResponseMessage resposta, RequisicaoCapturada destino)
        {
            _resposta = resposta;
            _destino = destino;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _destino.Metodo = request.Method;
            _destino.Url = request.RequestUri;
            _destino.EsquemaDoToken = request.Headers.Authorization?.Scheme;
            _destino.Token = request.Headers.Authorization?.Parameter;

            if (request.Content is not null)
            {
                _destino.Corpo = await request.Content.ReadAsStringAsync(ct);
            }

            return _resposta;
        }
    }

    private static HttpResponseMessage RespostaDeTexto(string texto) =>
        RespostaJson(new { choices = new[] { new { message = new { role = "assistant", content = texto }, finish_reason = "stop" } } });

    private static HttpResponseMessage RespostaJson(object conteudo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(conteudo), System.Text.Encoding.UTF8, "application/json")
        };

    private static (MelhoradorDeTextoOpenAiCompativel Servico, RequisicaoCapturada Capturada) Criar(
        OpcoesDeIa opcoes,
        HttpResponseMessage? resposta = null)
    {
        var capturada = new RequisicaoCapturada();
        var cliente = Testes.CriarHttpClient(
            new Capturador(resposta ?? RespostaDeTexto("texto novo"), capturada),
            "https://exemplo.invalido/");

        var servico = new MelhoradorDeTextoOpenAiCompativel(
            cliente,
            Options.Create(opcoes),
            NullLogger<MelhoradorDeTextoOpenAiCompativel>.Instance);

        return (servico, capturada);
    }

    private static (MelhoradorDeTextoOpenAiCompativel Servico, RequisicaoCapturada Capturada) Criar(
        OpcoesDeIa opcoes,
        HttpStatusCode status) => Criar(opcoes, new HttpResponseMessage(status) { Content = new StringContent("{}") });

    private static string MensagemDeSistema(string corpo)
    {
        using var json = JsonDocument.Parse(corpo);
        return json.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString()!;
    }

    private static string MensagemDoUsuario(string corpo)
    {
        using var json = JsonDocument.Parse(corpo);
        return json.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString()!;
    }

    [Fact]
    public async Task MelhorarAsync_devolve_o_texto_gerado_e_o_modelo()
    {
        var (servico, _) = Criar(CriarOpcoes());

        var resultado = await servico.MelhorarAsync("Batom Matte", DescricaoValida, CancellationToken.None);

        Assert.Equal("texto novo", resultado.DescricaoMelhorada);
        Assert.Equal("openai/gpt-oss-120b", resultado.ModeloUsado);
    }

    [Fact]
    public async Task MelhorarAsync_chama_chat_completions_do_provedor_configurado()
    {
        var (servico, capturada) = Criar(CriarOpcoes());

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, capturada.Metodo);
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_troca_de_provedor_pela_url_base()
    {
        var (servico, capturada) = Criar(CriarOpcoes(urlBase: "https://openrouter.ai/api/v1/"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_manda_a_chave_como_bearer_e_fora_da_url()
    {
        var (servico, capturada) = Criar(CriarOpcoes(chave: "gsk-segredo"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal("Bearer", capturada.EsquemaDoToken);
        Assert.Equal("gsk-segredo", capturada.Token);
        Assert.DoesNotContain("gsk-segredo", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_manda_o_modelo_configurado()
    {
        var (servico, capturada) = Criar(CriarOpcoes(modelo: "qwen/qwen3.8-27b"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        using var json = JsonDocument.Parse(capturada.Corpo);
        Assert.Equal("qwen/qwen3.8-27b", json.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task MelhorarAsync_usa_instrucao_de_sistema_com_as_regras_de_alegacao()
    {
        var (servico, capturada) = Criar(CriarOpcoes());

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        var sistema = MensagemDeSistema(capturada.Corpo);
        Assert.Contains("Não invente", sistema);
        Assert.Contains("resultado garantido", sistema);
        Assert.Contains("somente a descrição reescrita", sistema);
    }

    [Fact]
    public async Task MelhorarAsync_manda_nome_e_descricao_na_mensagem_de_usuario()
    {
        var (servico, capturada) = Criar(CriarOpcoes());

        await servico.MelhorarAsync("Batom Matte", DescricaoValida, CancellationToken.None);

        var usuario = MensagemDoUsuario(capturada.Corpo);
        Assert.Contains("Batom Matte", usuario);
        Assert.Contains(DescricaoValida, usuario);
    }

    [Fact]
    public async Task MelhorarAsync_nao_usa_temperatura_zero_que_o_provedor_rejeita()
    {
        var (servico, capturada) = Criar(CriarOpcoes());

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        using var json = JsonDocument.Parse(capturada.Corpo);
        var temperatura = json.RootElement.GetProperty("temperature").GetDouble();
        Assert.True(temperatura > 0, $"temperatura {temperatura} seria convertida pelo provedor");
        Assert.True(temperatura <= 2);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_chave_nao_esta_configurada()
    {
        var (servico, _) = Criar(CriarOpcoes(chave: "   "));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("não configurada", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_descricao_e_curta_demais()
    {
        var (servico, _) = Criar(CriarOpcoes());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", "curta", CancellationToken.None));

        Assert.Contains("pelo menos 10 caracteres", excecao.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("     ")]
    public async Task MelhorarAsync_lanca_quando_a_descricao_esta_vazia(string? descricao)
    {
        var (servico, _) = Criar(CriarOpcoes());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", descricao!, CancellationToken.None));

        Assert.Contains("pelo menos 10 caracteres", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_descricao_e_longa_demais()
    {
        var (servico, _) = Criar(CriarOpcoes());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", new string('a', 4_001), CancellationToken.None));

        Assert.Contains("passa de 4000 caracteres", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_aceita_descricao_no_limite()
    {
        var (servico, _) = Criar(CriarOpcoes());

        var resultado = await servico.MelhorarAsync("Batom", new string('a', 4_000), CancellationToken.None);

        Assert.Equal("texto novo", resultado.DescricaoMelhorada);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_chave_recusada_sem_repetir_o_erro_do_provedor()
    {
        var (servico, _) = Criar(
            CriarOpcoes(),
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"message\":\"Invalid API Key\"}}")
            });

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("chave da IA foi recusada", excecao.Message);
        Assert.DoesNotContain("Invalid API Key", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_cota_esgotada()
    {
        var (servico, _) = Criar(CriarOpcoes(), HttpStatusCode.TooManyRequests);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("cota gratuita da IA foi atingida", excecao.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task MelhorarAsync_traduz_modelo_inexistente(HttpStatusCode status)
    {
        var (servico, _) = Criar(CriarOpcoes(), status);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("modelo de IA configurado não existe", excecao.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task MelhorarAsync_traduz_erro_generico(HttpStatusCode status)
    {
        var (servico, _) = Criar(CriarOpcoes(), status);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("não conseguiu melhorar o texto", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_o_texto_vem_so_com_espacos()
    {
        var (servico, _) = Criar(
            CriarOpcoes(),
            RespostaJson(new
            {
                choices = new[] { new { message = new { role = "assistant", content = "   " }, finish_reason = "length" } }
            }));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("texto vazio", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_resposta_nao_tem_escolhas()
    {
        var (servico, _) = Criar(CriarOpcoes(), RespostaJson(new { choices = Array.Empty<object>() }));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("texto vazio", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_corta_nome_acima_do_limite()
    {
        var (servico, capturada) = Criar(CriarOpcoes());

        await servico.MelhorarAsync(new string('n', 200), DescricaoValida, CancellationToken.None);

        Assert.DoesNotContain(new string('n', 151), MensagemDoUsuario(capturada.Corpo));
    }
}
