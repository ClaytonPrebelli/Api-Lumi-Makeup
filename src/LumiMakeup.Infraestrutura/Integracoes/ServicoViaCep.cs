using System.Net.Http.Json;
using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class ServicoViaCep : IServicoViaCep
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServicoViaCep> _logger;

    public ServicoViaCep(HttpClient httpClient, ILogger<ServicoViaCep> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ResultadoViaCep?> ConsultarAsync(string cep, CancellationToken cancellationToken = default)
    {
        var cepLimpo = cep.Replace("-", string.Empty).Trim();

        try
        {
            var response = await _httpClient.GetFromJsonAsync<RespostaViaCep>(
                $"ws/{cepLimpo}/json/",
                cancellationToken);

            if (response is null || response.Erro)
            {
                return null;
            }

            return new ResultadoViaCep(
                response.Cep,
                response.Logradouro,
                response.Bairro,
                response.Localidade,
                response.Uf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar ViaCEP para CEP {Cep}", cepLimpo);
            return null;
        }
    }

    private sealed class RespostaViaCep
    {
        public string Cep { get; set; } = string.Empty;
        public string Logradouro { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Localidade { get; set; } = string.Empty;
        public string Uf { get; set; } = string.Empty;
        public bool Erro { get; set; }
    }
}