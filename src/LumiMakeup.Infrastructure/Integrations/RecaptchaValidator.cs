using System.Net.Http.Json;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class RecaptchaOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public double ScoreThreshold { get; set; } = 0.5;
}

public sealed class RecaptchaValidator : IRecaptchaValidator
{
    private readonly HttpClient _httpClient;
    private readonly RecaptchaOptions _options;
    private readonly ILogger<RecaptchaValidator> _logger;

    public RecaptchaValidator(
        HttpClient httpClient,
        IOptions<RecaptchaOptions> options,
        ILogger<RecaptchaValidator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            _logger.LogWarning("RecaptchaValidator: SecretKey não configurada — aceitando registro sem validação.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("RecaptchaValidator: token ausente.");
            return false;
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = _options.SecretKey,
            ["response"] = token
        });

        var response = await _httpClient.PostAsync("/recaptcha/api/siteverify", form, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("RecaptchaValidator: resposta inesperada do Google ({StatusCode}).", response.StatusCode);
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken: cancellationToken);
        if (result is null || !result.Success)
        {
            var details = result?.ErrorCodes is { Length: > 0 } ? string.Join(", ", result.ErrorCodes) : "sem detalhes";
            _logger.LogWarning("RecaptchaValidator: validação falhou ({Details}).", details);
            return false;
        }

        if (result.Score is not null && result.Score < _options.ScoreThreshold)
        {
            _logger.LogWarning("RecaptchaValidator: score {Score} abaixo do limite {Threshold}.", result.Score, _options.ScoreThreshold);
            return false;
        }

        return true;
    }

    private sealed class SiteVerifyResponse
    {
        public bool Success { get; set; }
        public double? Score { get; set; }
        public string[]? ErrorCodes { get; set; }
    }
}