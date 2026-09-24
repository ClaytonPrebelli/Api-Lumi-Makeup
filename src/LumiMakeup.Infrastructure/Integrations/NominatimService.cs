using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Web;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class NominatimService : INominatimService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimService> _logger;

    public NominatimService(HttpClient httpClient, ILogger<NominatimService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default)
    {
        var consulta = $"q={HttpUtility.UrlEncode(endereco)}&format=json&limit=1";

        try
        {
            var resultados = await _httpClient.GetFromJsonAsync<List<ResultadoNominatim>>(consulta, cancellationToken);

            if (resultados is null || resultados.Count == 0)
            {
                return null;
            }

            return (decimal.Parse(resultados[0].Latitude, System.Globalization.CultureInfo.InvariantCulture),
                    decimal.Parse(resultados[0].Longitude, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao geocodificar endereço no Nominatim: {Endereco}", endereco);
            return null;
        }
    }

    private sealed class ResultadoNominatim
    {
        [JsonPropertyName("lat")]
        public string Latitude { get; set; } = "0";

        [JsonPropertyName("lon")]
        public string Longitude { get; set; } = "0";
    }
}