using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class OpcoesDeIa
{
    public string Chave { get; set; } = string.Empty;
    public string UrlBase { get; set; } = "https://api.groq.com/openai/v1";
    public string Modelo { get; set; } = "openai/gpt-oss-120b";
    public double Temperatura { get; set; } = 0.3;
    public int MaximoDeTokens { get; set; } = 1_024;
    public int TimeoutEmSegundos { get; set; } = 45;
}

/// <summary>
/// Reescreve a descrição de produto usando um endpoint compatível com a API da OpenAI.
/// </summary>
/// <remarks>
/// Groq, OpenRouter, Cerebras e NVIDIA NIM falam o mesmo formato de
/// <c>POST {UrlBase}/chat/completions</c>. Implementar só esse formato deixa o provedor
/// e o modelo como configuração, em vez de uma classe por fornecedor. Trocar de
/// provedor é mudar <see cref="OpcoesDeIa.UrlBase" />.
/// </remarks>
public sealed class MelhoradorDeTextoOpenAiCompativel : IMelhoradorDeTextoService
{
    private readonly HttpClient _httpClient;
    private readonly OpcoesDeIa _opcoes;
    private readonly ILogger<MelhoradorDeTextoOpenAiCompativel> _logger;

    public MelhoradorDeTextoOpenAiCompativel(
        HttpClient httpClient,
        IOptions<OpcoesDeIa> opcoes,
        ILogger<MelhoradorDeTextoOpenAiCompativel> logger)
    {
        _httpClient = httpClient;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task<ResultadoDeMelhoriaDeTexto> MelhorarAsync(
        string nome,
        string descricao,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.Chave))
        {
            throw new InvalidOperationException(
                "Melhoria de texto com IA não configurada — defina ExternalServices:Ia:Chave.");
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

        using var requisicao = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(_opcoes.UrlBase.TrimEnd('/') + "/"), "chat/completions"))
        {
            Content = JsonContent.Create(new RequisicaoDeChat
            {
                Model = _opcoes.Modelo,
                Temperature = _opcoes.Temperatura,
                MaxTokens = _opcoes.MaximoDeTokens,
                Messages =
                [
                    new Mensagem { Role = "system", Content = InstrucaoDoSistema },
                    new Mensagem
                    {
                        Role = "user",
                        Content = $"Nome do produto: {(nomeLimpo.Length == 0 ? "(não informado)" : nomeLimpo)}\n\nDescrição original:\n{descricaoLimpa}"
                    }
                ]
            })
        };

        requisicao.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _opcoes.Chave);

        using var resposta = await _httpClient.SendAsync(requisicao, cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "{Provedor} respondeu {StatusCode} (modelo {Modelo}): {Corpo}",
                HostDaUrl(_opcoes.UrlBase),
                resposta.StatusCode,
                _opcoes.Modelo,
                Resumir(corpo));

            throw new InvalidOperationException(MensagemDeErro(resposta.StatusCode));
        }

        var resultado = await resposta.Content
            .ReadFromJsonAsync<RespostaDeChat>(cancellationToken: cancellationToken);

        var texto = resultado?.Escolhas?
            .FirstOrDefault()?
            .Mensagem?
            .Content?
            .Trim();

        if (string.IsNullOrWhiteSpace(texto))
        {
            var motivo = resultado?.Escolhas?.FirstOrDefault()?.FinishReason;
            _logger.LogWarning("{Provedor} devolveu conteúdo vazio (finish_reason={Motivo}).", HostDaUrl(_opcoes.UrlBase), motivo ?? "(nenhum)");
            throw new InvalidOperationException("A IA devolveu um texto vazio. Tente novamente.");
        }

        _logger.LogInformation(
            "Descrição reescrita por {Modelo} via {Provedor} ({Caracteres} caracteres).",
            _opcoes.Modelo,
            HostDaUrl(_opcoes.UrlBase),
            texto.Length);

        return new ResultadoDeMelhoriaDeTexto(texto, _opcoes.Modelo);
    }

    private static string MensagemDeErro(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
            "A chave da IA foi recusada. Confira ExternalServices:Ia:Chave.",
        HttpStatusCode.TooManyRequests =>
            "A cota gratuita da IA foi atingida. Tente de novo mais tarde.",
        HttpStatusCode.NotFound or HttpStatusCode.BadRequest =>
            "O modelo de IA configurado não existe ou não está disponível para esta chave. Confira ExternalServices:Ia:Modelo.",
        _ => "A IA não conseguiu melhorar o texto agora. Tente novamente."
    };

    private static string HostDaUrl(string urlBase) =>
        Uri.TryCreate(urlBase, UriKind.Absolute, out var uri) ? uri.Host : "(provedor desconhecido)";

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
        4. Não invente número de cores, gramas, volume ou prazo de duração.
        5. Escreva em português do Brasil, em parágrafos curtos, com o tom de quem usa o produto.
        6. Devolva somente a descrição reescrita. Sem título, sem aspas, sem comentário,
           sem explicar o que você mudou e sem repetir o nome do produto.

        Se a descrição original já estiver boa, devolva ela mesma com pequenos ajustes de escrita.
        """;

    private sealed class RequisicaoDeChat
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<Mensagem> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private sealed class Mensagem
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class RespostaDeChat
    {
        [JsonPropertyName("choices")]
        public List<Escolha>? Escolhas { get; set; }
    }

    private sealed class Escolha
    {
        [JsonPropertyName("message")]
        public Mensagem? Mensagem { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }
}
