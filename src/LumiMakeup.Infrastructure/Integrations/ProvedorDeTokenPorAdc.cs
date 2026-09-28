using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Obtém um token OAuth2 do Google a partir das credenciais padrão da aplicação (ADC).
/// </summary>
/// <remarks>
/// Existe como abstração porque <c>GoogleCredential.GetApplicationDefaultAsync</c> só
/// funciona em uma máquina com ADC configurado. Nos testes, sem ADC, a implementação
/// real lançaria. A separação permite injetar um dublê.
/// </remarks>
public interface IProvedorDeTokenDoGoogle
{
    Task<string> ObterTokenAsync(string escopo, CancellationToken cancellationToken = default);
}

public sealed class ProvedorDeTokenPorAdc : IProvedorDeTokenDoGoogle
{
    private const string MensagemDeSemCredencial =
        "Sem credenciais do Google. Rode 'gcloud auth application-default login' ou defina GOOGLE_APPLICATION_CREDENTIALS.";

    private readonly ILogger<ProvedorDeTokenPorAdc> _logger;

    private GoogleCredential? _credencial;
    private readonly SemaphoreSlim _trava = new(1, 1);

    public ProvedorDeTokenPorAdc(ILogger<ProvedorDeTokenPorAdc> logger)
    {
        _logger = logger;
    }

    public async Task<string> ObterTokenAsync(string escopo, CancellationToken cancellationToken = default)
    {
        await _trava.WaitAsync(cancellationToken);

        try
        {
            if (_credencial is null)
            {
                try
                {
                    _credencial = await GoogleCredential.GetApplicationDefaultAsync();
                    _logger.LogInformation("Credenciais padrão do Google carregadas.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Não foi possível carregar as credenciais padrão do Google.");
                    throw new InvalidOperationException(MensagemDeSemCredencial);
                }
            }

            return await _credencial
                .CreateScoped(escopo)
                .UnderlyingCredential
                .GetAccessTokenForRequestAsync();
        }
        finally
        {
            _trava.Release();
        }
    }
}
