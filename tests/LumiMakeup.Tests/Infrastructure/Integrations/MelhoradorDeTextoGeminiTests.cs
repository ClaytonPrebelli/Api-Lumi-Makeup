using System.Net;
using System.Text.Json;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class MelhoradorDeTextoGeminiTests
{
    private const string DescricaoValida = "Batom matte de longa duracao, cor vinho";

    private static GeminiOptions OpcoesDaAiStudio(string chave = "chave-de-teste", string modelo = "gemini-3.5-flash") =>
        new() { Chave = chave, Modelo = modelo };

    private static GeminiOptions OpcoesDoVertex(string projeto = "meu-projeto", string modelo = "gemini-3.5-flash") =>
        new() { Chave = string.Empty, Projeto = projeto, Local = "global", Modelo = modelo };

    private static HttpResponseMessage RespostaDeTexto(string texto) =>
        RespostaJson(new { candidates = new[] { new { content = new { parts = new[] { new { text = texto } } } } } });

    private static HttpResponseMessage RespostaJson(object conteudo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(conteudo), System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class RequisicaoCapturada
    {
        public HttpMethod? Metodo { get; set; }
        public Uri? Url { get; set; }
        public string Corpo { get; set; } = string.Empty;
        public bool TemCabecalhoDaChave { get; set; }
        public string? ValorDoCabecalhoDaChave { get; set; }
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
            _destino.TemCabecalhoDaChave = request.Headers.Contains("x-goog-api-key");

            if (request.Headers.TryGetValues("x-goog-api-key", out var valores))
            {
                _destino.ValorDoCabecalhoDaChave = valores.FirstOrDefault();
            }

            _destino.EsquemaDoToken = request.Headers.Authorization?.Scheme;
            _destino.Token = request.Headers.Authorization?.Parameter;

            if (request.Content is not null)
            {
                _destino.Corpo = await request.Content.ReadAsStringAsync(ct);
            }

            return _resposta;
        }
    }

    private sealed class ProvedorDeTokenFalso : IProvedorDeTokenDoGoogle
    {
        private readonly string? _token;

        public ProvedorDeTokenFalso(string? token) => _token = token;

        public Task<string> ObterTokenAsync(string escopo, CancellationToken cancellationToken = default)
        {
            if (_token is null)
            {
                throw new InvalidOperationException(
                    "Sem credenciais do Google. Rode 'gcloud auth application-default login' ou defina GOOGLE_APPLICATION_CREDENTIALS.");
            }

            return Task.FromResult(_token);
        }
    }

    private static IProvedorDeTokenDoGoogle CredencialFalsa(string? token) => new ProvedorDeTokenFalso(token);

    private static string InstrucaoDoSistemaEnviada(string corpo)
    {
        using var json = JsonDocument.Parse(corpo);
        return json.RootElement
            .GetProperty("systemInstruction")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()!;
    }

    private static (MelhoradorDeTextoGemini Servico, RequisicaoCapturada Capturada) Criar(
        GeminiOptions opcoes,
        HttpResponseMessage? resposta = null,
        IProvedorDeTokenDoGoogle? credencial = null)
    {
        var capturada = new RequisicaoCapturada();
        var cliente = Testes.CriarHttpClient(
            new Capturador(resposta ?? RespostaDeTexto("texto novo"), capturada),
            "https://exemplo.invalido/");

        var servico = new MelhoradorDeTextoGemini(
            cliente,
            Options.Create(opcoes),
            credencial ?? CredencialFalsa("token-adc"),
            NullLogger<MelhoradorDeTextoGemini>.Instance);

        return (servico, capturada);
    }

    private static (MelhoradorDeTextoGemini Servico, RequisicaoCapturada Capturada) Criar(
        GeminiOptions opcoes,
        HttpStatusCode status) => Criar(opcoes, new HttpResponseMessage(status) { Content = new StringContent("{}") });

    [Fact]
    public async Task MelhorarAsync_devolve_o_texto_gerado_e_o_modelo()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio());

        var resultado = await servico.MelhorarAsync("Batom Matte", DescricaoValida, CancellationToken.None);

        Assert.Equal("texto novo", resultado.DescricaoMelhorada);
        Assert.Equal("gemini-3.5-flash", resultado.ModeloUsado);
    }

    [Fact]
    public async Task MelhorarAsync_chama_o_endpoint_do_modelo_configurado()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio(modelo: "gemini-3.8-flash"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, capturada.Metodo);
        Assert.Contains("gemini-3.8-flash:generateContent", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_manda_a_chave_no_cabecalho_e_nao_na_url()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio(chave: "segredo-123"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.True(capturada.TemCabecalhoDaChave);
        Assert.Equal("segredo-123", capturada.ValorDoCabecalhoDaChave);
        Assert.DoesNotContain("segredo-123", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_envia_nome_e_descricao_no_corpo()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio());

        await servico.MelhorarAsync("Batom Matte", DescricaoValida, CancellationToken.None);

        Assert.Contains(DescricaoValida, capturada.Corpo);
        Assert.Contains("Batom Matte", capturada.Corpo);
    }

    [Fact]
    public async Task MelhorarAsync_proibe_inventar_alegacao_no_prompt()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio());

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        var instrucao = InstrucaoDoSistemaEnviada(capturada.Corpo);
        Assert.Contains("Não invente", instrucao);
        Assert.Contains("resultado garantido", instrucao);
    }

    [Fact]
    public async Task MelhorarAsync_pede_para_nao_devolver_comentario_alem_do_texto()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio());

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Contains("somente a descrição reescrita", InstrucaoDoSistemaEnviada(capturada.Corpo));
    }

    [Fact]
    public async Task MelhorarAsync_manda_a_descricao_como_conteudo_de_usuario()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio());

        await servico.MelhorarAsync("Batom Matte", DescricaoValida, CancellationToken.None);

        using var json = JsonDocument.Parse(capturada.Corpo);
        var conteudo = json.RootElement.GetProperty("contents")[0];
        Assert.Equal("user", conteudo.GetProperty("role").GetString());
        var texto = conteudo.GetProperty("parts")[0].GetProperty("text").GetString()!;
        Assert.Contains(DescricaoValida, texto);
        Assert.Contains("Batom Matte", texto);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_chave_nao_esta_configurada()
    {
        var (servico, _) = Criar(OpcoesDoVertex(projeto: ""));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("não configurada", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_descricao_e_curta_demais()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio());

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
        var (servico, _) = Criar(OpcoesDaAiStudio());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", descricao!, CancellationToken.None));

        Assert.Contains("pelo menos 10 caracteres", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_descricao_e_longa_demais()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", new string('a', 4_001), CancellationToken.None));

        Assert.Contains("passa de 4000 caracteres", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_aceita_descricao_no_limite()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio());

        var resultado = await servico.MelhorarAsync("Batom", new string('a', 4_000), CancellationToken.None);

        Assert.Equal("texto novo", resultado.DescricaoMelhorada);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_chave_recusada_sem_repetir_o_erro_do_google()
    {
        var (servico, _) = Criar(
            OpcoesDaAiStudio(),
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"message\":\"API key not valid\"}}")
            });

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("chave da API do Google foi recusada", excecao.Message);
        Assert.DoesNotContain("API key not valid", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_cota_esgotada()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio(), HttpStatusCode.TooManyRequests);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("cota do Google foi atingida", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_modelo_inexistente()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio(), HttpStatusCode.NotFound);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("modelo configurado não existe", excecao.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task MelhorarAsync_traduz_erro_generico(HttpStatusCode status)
    {
        var (servico, _) = Criar(OpcoesDaAiStudio(), status);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("não conseguiu melhorar o texto", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_o_google_bloqueia_a_geracao()
    {
        var (servico, _) = Criar(
            OpcoesDaAiStudio(),
            RespostaJson(new
            {
                promptFeedback = new { blockReason = "SAFETY" },
                candidates = Array.Empty<object>()
            }));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("recusou gerar", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_o_texto_vem_so_com_espacos()
    {
        var (servico, _) = Criar(
            OpcoesDaAiStudio(),
            RespostaJson(new
            {
                candidates = new[]
                {
                    new { content = new { parts = new[] { new { text = "   " } } }, finishReason = "MAX_TOKENS" }
                }
            }));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("texto vazio", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_a_resposta_nao_tem_candidatos()
    {
        var (servico, _) = Criar(OpcoesDaAiStudio(), RespostaJson(new { candidates = Array.Empty<object>() }));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("texto vazio", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_corta_nome_acima_do_limite()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio());

        await servico.MelhorarAsync(new string('n', 200), DescricaoValida, CancellationToken.None);

        Assert.DoesNotContain(new string('n', 151), capturada.Corpo);
    }

    [Fact]
    public async Task MelhorarAsync_com_chave_monta_a_url_da_ai_studio()
    {
        var (servico, capturada) = Criar(OpcoesDaAiStudio(modelo: "gemini-3.8-flash"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.8-flash:generateContent", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_sem_chave_monta_a_url_do_agent_platform_com_projeto_e_regiao()
    {
        var (servico, capturada) = Criar(
            new GeminiOptions
            {
                Projeto = "loja-lumi",
                Local = "us-central1",
                Modelo = "gemini-3.5-flash"
            },
            credencial: CredencialFalsa("token-adc"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal(
            "https://aiplatform.googleapis.com/v1/projects/loja-lumi/locations/us-central1/publishers/google/models/gemini-3.5-flash:generateContent",
            capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_sem_chave_usa_global_quando_nenhuma_regiao_e_informada()
    {
        var (servico, capturada) = Criar(
            new GeminiOptions { Projeto = "loja-lumi" },
            credencial: CredencialFalsa("token-adc"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Contains("/locations/global/", capturada.Url!.ToString());
    }

    [Fact]
    public async Task MelhorarAsync_sem_chave_manda_authorization_bearer_e_nao_a_chave_da_ai_studio()
    {
        var (servico, capturada) = Criar(
            OpcoesDoVertex(),
            credencial: CredencialFalsa("token-adc-123"));

        await servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None);

        Assert.Equal("Bearer", capturada.EsquemaDoToken);
        Assert.Equal("token-adc-123", capturada.Token);
        Assert.False(capturada.TemCabecalhoDaChave);
    }

    [Fact]
    public async Task MelhorarAsync_lanca_quando_nao_ha_chave_nem_projeto_configurado()
    {
        var (servico, _) = Criar(new GeminiOptions());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("não configurada", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_credencial_recusada_no_modo_adc()
    {
        var (servico, _) = Criar(
            OpcoesDoVertex(),
            credencial: CredencialFalsa("token-adc"),
            resposta: new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":{\"message\":\"permission denied\"}}")
            });

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("recusou as credenciais", excecao.Message);
        Assert.DoesNotContain("permission denied", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_modelo_ou_regiao_invalida_no_modo_adc()
    {
        var (servico, _) = Criar(
            OpcoesDoVertex(),
            credencial: CredencialFalsa("token-adc"),
            resposta: new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}")
            });

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("modelo ou a região não existem", excecao.Message);
    }

    [Fact]
    public async Task MelhorarAsync_traduz_tranquilmente_quando_o_adc_nao_existe()
    {
        var (servico, _) = Criar(OpcoesDoVertex(), credencial: CredencialFalsa(null));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.MelhorarAsync("Batom", DescricaoValida, CancellationToken.None));

        Assert.Contains("application-default login", excecao.Message);
    }
}
