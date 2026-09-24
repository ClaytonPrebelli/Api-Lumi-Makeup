using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class OpcoesDeRecaptcha
{
    public string ChaveSecreta { get; set; } = string.Empty;
    public double LimiteDeScore { get; set; } = 0.5;
}

public sealed class ValidadorDeRecaptcha : IValidadorDeRecaptcha
{
    private readonly HttpClient _httpClient;
    private readonly OpcoesDeRecaptcha _opcoes;
    private readonly ILogger<ValidadorDeRecaptcha> _logger;

    public ValidadorDeRecaptcha(
        HttpClient httpClient,
        IOptions<OpcoesDeRecaptcha> opcoes,
        ILogger<ValidadorDeRecaptcha> logger)
    {
        _httpClient = httpClient;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task<bool> ValidarTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ChaveSecreta))
        {
            _logger.LogWarning("ValidadorDeRecaptcha: ChaveSecreta não configurada — aceitando registro sem validação.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("ValidadorDeRecaptcha: token ausente.");
            return false;
        }

        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = _opcoes.ChaveSecreta,
            ["response"] = token
        });

        var response = await _httpClient.PostAsync("/recaptcha/api/siteverify", formulario, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("ValidadorDeRecaptcha: resposta inesperada do Google ({StatusCode}).", response.StatusCode);
            return false;
        }

        var resultado = await response.Content.ReadFromJsonAsync<RespostaSiteVerify>(cancellationToken: cancellationToken);
        if (resultado is null || !resultado.Sucesso)
        {
            var detalhes = resultado?.CodigosDeErro is { Length: > 0 } ? string.Join(", ", resultado.CodigosDeErro) : "sem detalhes";
            _logger.LogWarning("ValidadorDeRecaptcha: validação falhou ({Detalhes}).", detalhes);
            return false;
        }

        if (resultado.Score is not null && resultado.Score < _opcoes.LimiteDeScore)
        {
            _logger.LogWarning("ValidadorDeRecaptcha: score {Score} abaixo do limite {Limite}.", resultado.Score, _opcoes.LimiteDeScore);
            return false;
        }

        return true;
    }

    private sealed class RespostaSiteVerify
    {
        [JsonPropertyName("success")]
        public bool Sucesso { get; set; }

        [JsonPropertyName("score")]
        public double? Score { get; set; }

        [JsonPropertyName("error-codes")]
        public string[]? CodigosDeErro { get; set; }
    }
}