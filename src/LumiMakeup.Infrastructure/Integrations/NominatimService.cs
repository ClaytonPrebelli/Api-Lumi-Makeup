using System.Net.Http.Json;
using System.Text;
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

    public async Task<(decimal Latitude, decimal Longitude)?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        var query = $"q={HttpUtility.UrlEncode(address)}&format=json&limit=1";

        try
        {
            var results = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(query, cancellationToken);

            if (results is null || results.Count == 0)
            {
                return null;
            }

            return (decimal.Parse(results[0].Lat, System.Globalization.CultureInfo.InvariantCulture),
                    decimal.Parse(results[0].Lon, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao geocodificar endereço no Nominatim: {Address}", address);
            return null;
        }
    }

    private sealed class NominatimResult
    {
        public string Lat { get; set; } = "0";
        public string Lon { get; set; } = "0";
    }
}