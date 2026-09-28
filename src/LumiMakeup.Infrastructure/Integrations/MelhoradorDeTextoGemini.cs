using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class GeminiOptions
{
    public string Chave { get; set; } = string.Empty;
    public string Projeto { get; set; } = string.Empty;
    public string Local { get; set; } = "global";
    public string Modelo { get; set; } = "gemini-3.5-flash";
    public string EndpointDaAiStudio { get; set; } = "https://generativelanguage.googleapis.com/";
    public string EndpointDoVertex { get; set; } = "https://aiplatform.googleapis.com/";
    public int TimeoutEmSegundos { get; set; } = 30;
}

public enum ModoDeAcessoAoGemini
{
    ChaveDaAiStudio,
    CredenciaisPadrao
}

public sealed class MelhoradorDeTextoGemini : IMelhoradorDeTextoService
{
    private const string CabecalhoDaChave = "x-goog-api-key";
    private const string EscopoDoVertex = "https://www.googleapis.com/auth/cloud-platform";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _opcoes;
    private readonly IProvedorDeTokenDoGoogle _provedorDeToken;
    private readonly ILogger<MelhoradorDeTextoGemini> _logger;

    public MelhoradorDeTextoGemini(
        HttpClient httpClient,
        IOptions<GeminiOptions> opcoes,
        IProvedorDeTokenDoGoogle provedorDeToken,
        ILogger<MelhoradorDeTextoGemini> logger)
    {
        _httpClient = httpClient;
        _opcoes = opcoes.Value;
        _provedorDeToken = provedorDeToken;
        _logger = logger;
    }

    private ModoDeAcessoAoGemini Modo =>
        string.IsNullOrWhiteSpace(_opcoes.Chave) ? ModoDeAcessoAoGemini.CredenciaisPadrao : ModoDeAcessoAoGemini.ChaveDaAiStudio;

    public async Task<ResultadoDeMelhoriaDeTexto> MelhorarAsync(
        string nome,
        string descricao,
        CancellationToken cancellationToken = default)
    {
        if (Modo == ModoDeAcessoAoGemini.CredenciaisPadrao && string.IsNullOrWhiteSpace(_opcoes.Projeto))
        {
            throw new InvalidOperationException(
                "Melhoria de texto com IA não configurada — defina ExternalServices:Gemini:Chave (AI Studio) ou ExternalServices:Gemini:Projeto (com ADC).");
        }

        var descricaoLimpa = (descricao ?? string.Empty).Trim();

        if (descricaoLimpa.Length < LimitesDeMelhoriaDeTexto.MinimoDeCaracteresDaDescricao)
        {
            throw new InvalidOperationException(
                $"Escreva pelo menos {LimitesDeMelhoriaDeTexto.MinimoDeCaracteresDaDescricao} caracteres antes de pedir a melhoria.");
        }

        if (descricaoLimpa.Length > LimitesDeMelhoriaDeTexto.MaximoDeCaracteresDaDescricao)
        {
            throw new InvalidOperationException(
                $"A descrição passa de {LimitesDeMelhoriaDeTexto.MaximoDeCaracteresDaDescricao} caracteres.");
        }

        var nomeLimpo = (nome ?? string.Empty).Trim();
        if (nomeLimpo.Length > LimitesDeMelhoriaDeTexto.MaximoDeCaracteresDoNome)
        {
            nomeLimpo = nomeLimpo[..LimitesDeMelhoriaDeTexto.MaximoDeCaracteresDoNome];
        }

        using var requisicao = new HttpRequestMessage(HttpMethod.Post, MontarUrl())
        {
            Content = JsonContent.Create(new RequisicaoGenerateContent
            {
                SystemInstruction = new Conteudo
                {
                    Parts = [new Parte { Text = InstrucaoDoSistema }]
                },
                Contents =
                [
                    new Conteudo
                    {
                        Role = "user",
                        Parts =
                        [
                            new Parte
                            {
                                Text = $"Nome do produto: {(nomeLimpo.Length == 0 ? "(não informado)" : nomeLimpo)}\n\nDescrição original:\n{descricaoLimpa}"
                            }
                        ]
                    }
                ],
                GenerationConfig = new ConfiguracaoDeGeracao
                {
                    MaxOutputTokens = 1_024,
                    ThinkingConfig = new ConfiguracaoDePensamento { ThinkingLevel = "low" }
                }
            })
        };

        await AplicarAutenticacaoAsync(requisicao, cancellationToken);

        using var resposta = await _httpClient.SendAsync(requisicao, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Gemini respondeu {StatusCode} ({Modo}): {Corpo}",
                resposta.StatusCode,
                Modo,
                Resumir(corpo));

            throw new InvalidOperationException(MensagemDeErro(resposta.StatusCode, Modo));
        }

        var resultado = await resposta.Content
            .ReadFromJsonAsync<RespostaGenerateContent>(cancellationToken: cancellationToken);

        if (resultado?.PromptFeedback?.BlockReason is { Length: > 0 } bloqueio)
        {
            _logger.LogWarning("Gemini bloqueou a geração ({Bloqueio}).", bloqueio);
            throw new InvalidOperationException("O Google recusou gerar esse texto. Tente reescrever o texto original.");
        }

        var texto = resultado?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .FirstOrDefault()?
            .Text?
            .Trim();

        if (string.IsNullOrWhiteSpace(texto))
        {
            var motivo = resultado?.Candidates?.FirstOrDefault()?.FinishReason;
            _logger.LogWarning("Gemini devolveu conteúdo vazio (finishReason={Motivo}).", motivo ?? "(nenhum)");
            throw new InvalidOperationException("A IA devolveu um texto vazio. Tente novamente.");
        }

        _logger.LogInformation(
            "Descrição reescrita pelo modelo {Modelo} via {Modo} ({Caracteres} caracteres).",
            _opcoes.Modelo,
            Modo,
            texto.Length);

        return new ResultadoDeMelhoriaDeTexto(texto, _opcoes.Modelo);
    }

    private string MontarUrl()
    {
        if (Modo == ModoDeAcessoAoGemini.ChaveDaAiStudio)
        {
            return new Uri(new Uri(_opcoes.EndpointDaAiStudio), $"v1beta/models/{_opcoes.Modelo}:generateContent").ToString();
        }

        var caminho = $"v1/projects/{_opcoes.Projeto}/locations/{_opcoes.Local}/publishers/google/models/{_opcoes.Modelo}:generateContent";
        return new Uri(new Uri(_opcoes.EndpointDoVertex), caminho).ToString();
    }

    private async Task AplicarAutenticacaoAsync(HttpRequestMessage requisicao, CancellationToken cancellationToken)
    {
        if (Modo == ModoDeAcessoAoGemini.ChaveDaAiStudio)
        {
            requisicao.Headers.Add(CabecalhoDaChave, _opcoes.Chave);
            return;
        }

        var token = await _provedorDeToken.ObterTokenAsync(EscopoDoVertex, cancellationToken);
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string MensagemDeErro(HttpStatusCode status, ModoDeAcessoAoGemini modo) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => modo == ModoDeAcessoAoGemini.ChaveDaAiStudio
            ? "A chave da API do Google foi recusada. Confira ExternalServices:Gemini:Chave."
            : "O Google recusou as credenciais. Verifique o ADC e se a conta tem a permissão de chamar o modelo.",
        HttpStatusCode.TooManyRequests =>
            "A cota do Google foi atingida. Tente de novo mais tarde.",
        HttpStatusCode.NotFound or HttpStatusCode.BadRequest => modo == ModoDeAcessoAoGemini.CredenciaisPadrao
            ? "O modelo ou a região não existem para este projeto. Confira ExternalServices:Gemini:Modelo e Local."
            : "O modelo configurado não existe ou não está disponível. Confira ExternalServices:Gemini:Modelo.",
        _ => "A IA não conseguiu melhorar o texto agora. Tente novamente."
    };

    private static string Resumir(string corpo) =>
        corpo.Length <= 300 ? corpo : corpo[..300] + "...";

    private const string InstrucaoDoSistema = """
        Você reescreve descrições de produtos de cosméticos para a loja Lumi Makeup, no Brasil.

        Regras que você não pode quebrar:

        1. Não invente. Use apenas característica, ingrediente, textura, acabamento ou benefício
           que estejam escritos na descrição original. Se a pessoa não escreveu, você não escreve.
        2. Não use promessa de resultado garantido, nem linguagem que sugira efeito médico ou
           tratamento de doença.
        3. Não cite porcentagem, selo, aprovação, certificação, prêmio nem estudo clínico.
        4. Não invente número de cores, grams, volume ou prazo de duração.
        5. Escreva em português do Brasil, em parágrafos curtos, com o tom de quem usa o produto.
        6. Devolva somente a descrição reescrita. Sem título, sem aspas, sem comentário,
           sem explicar o que você mudou e sem repetir o nome do produto.

        Se a descrição original já estiver boa, devolva ela mesma com pequenos ajustes de escrita.
        """;

    private sealed class RequisicaoGenerateContent
    {
        [JsonPropertyName("systemInstruction")]
        public Conteudo? SystemInstruction { get; set; }

        [JsonPropertyName("contents")]
        public List<Conteudo> Contents { get; set; } = [];

        [JsonPropertyName("generationConfig")]
        public ConfiguracaoDeGeracao? GenerationConfig { get; set; }
    }

    private sealed class ConfiguracaoDeGeracao
    {
        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        [JsonPropertyName("thinkingConfig")]
        public ConfiguracaoDePensamento? ThinkingConfig { get; set; }
    }

    private sealed class ConfiguracaoDePensamento
    {
        [JsonPropertyName("thinkingLevel")]
        public string ThinkingLevel { get; set; } = "low";
    }

    private sealed class Conteudo
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("parts")]
        public List<Parte> Parts { get; set; } = [];
    }

    private sealed class Parte
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    private sealed class RespostaGenerateContent
    {
        [JsonPropertyName("candidates")]
        public List<Candidato>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public FeedbackDoPrompt? PromptFeedback { get; set; }
    }

    private sealed class Candidato
    {
        [JsonPropertyName("content")]
        public Conteudo? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }
    }

    private sealed class FeedbackDoPrompt
    {
        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }
}
