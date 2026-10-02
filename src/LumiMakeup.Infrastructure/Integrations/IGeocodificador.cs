using System.Globalization;
using System.Text.Json;
using System.Web;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Geocodifica├º├úo por provedores em cadeia.
///
/// O primeiro servi├ºo - o OpenStreetMap/Nominatim - respondia ponto a ponto,
/// mas o Windows Server de produ├º├úo n├úo consegue fechar TLS com o proxy dele:
/// a negocia├º├úo ├® recusada em todas as vers├Áes. Como o c├ílculo de frete ├® o
/// cora├º├úo da venda, ficar sem coordenada ├® perder o pedido, e por isso h├í
/// outros dois provedores atr├ís.
///
/// A ordem n├úo ├® por prefer├¬ncia: Photon devolve o logradouro como via
/// ('osm_type' = W) quando o endere├ºo tem rua, e o CEP exato quando n├úo tem -
/// ├® o ├║nico que resolve os dois casos sem chave. O ArcGIS entra depois
/// porque usa a gazeta pr├│pria da Esri e diverge do OSM em centenas de metros
/// quando cai no mesmo ponto.
///
/// Nenhum deles ├® inventar posi├º├úo: se todos recusarem, o c├ílculo recusa, e
/// melhor recusar do que cobrar por um lugar em que o cliente n├úo est├í.
/// </summary>
public sealed class GeocodificadorEmCadeia : IGeocodificador
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeocodificadorEmCadeia> _logger;

    public GeocodificadorEmCadeia(HttpClient httpClient, ILogger<GeocodificadorEmCadeia> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(
        string endereco,
        CancellationToken cancellationToken = default)
    {
        var cep = ExtrairCep(endereco);

        if (cep is null)
        {
            _logger.LogWarning("Endereco sem CEP, impossivel geocodificar: {Endereco}", endereco);
            UltimaFalha = $"endereco sem CEP: '{endereco}'";
            return null;
        }

        // O CEP vem antes da rua de proposito. Dois CEPs da mesma cidade caem no
        // mesmo centroide quando a busca e' por cidade, e a distancia entre eles
        // daria zero - o que faria todo cliente da cidade pagar a taxa minima
        // como se morasse ao lado. O CEP identifica um lugar unico.
        var porCep = await ConsultarPhotonAsync($"postalcode {cep}", endereco, cancellationToken)
            ?? await ConsultarArcGisAsync(cep, endereco, cancellationToken);

        if (porCep is not null)
        {
            return porCep;
        }

        // Sem o CEP no mapa, tenta o endere├ºo completo: Photon pode ter a rua
        // mesmo quando o CEP nao esta mapeado como ponto.
        var porEndereco = await ConsultarPhotonAsync(endereco, endereco, cancellationToken);

        if (porEndereco is not null)
        {
            return porEndereco;
        }

        _logger.LogError("Nenhum provedor de mapa localizou o endereco {Endereco}.", endereco);

        return null;
    }

    public string? UltimaFalha { get; private set; }

    public async Task<IReadOnlyList<object>> TestarProvedoresAsync(
        string cep,
        CancellationToken cancellationToken = default)
    {
        var resultados = new List<object>();
        var limpo = new string((cep ?? string.Empty).Where(char.IsDigit).ToArray());

        if (limpo.Length != 8)
        {
            return
            [
                new { provedor = "(entrada)", resultado = "CEP invalido" }
            ];
        }

        var formatado = $"{limpo[..5]}-{limpo[5..]}";

        var provedores = new (string Nome, Func<string, CancellationToken, Task<(decimal Latitude, decimal Longitude)?>> Consultar)[]
        {
            ("photon (komoot)", (c, ct) => ConsultarPhotonAsync($"postalcode {c}", c, ct)),
            ("arcgis", (c, ct) => ConsultarArcGisAsync(c, c, ct)),
            ("photon por endereco", (c, ct) => ConsultarPhotonAsync($"Rua, bairro, {c}", c, ct))
        };

        foreach (var (nome, consultar) in provedores)
        {
            try
            {
                var ponto = await consultar(formatado, cancellationToken);

                resultados.Add(ponto is null
                    ? new { provedor = nome, resultado = "sem ponto" }
                    : new { provedor = nome, resultado = "encontrado", latitude = ponto.Value.Latitude, longitude = ponto.Value.Longitude });
            }
            catch (Exception ex)
            {
                // A mensagem vem junto porque 'InvalidOperationException' sem
                // causa n├úo diz nada: foi o que apareceu quando o cliente ficou
                // sem BaseAddress, e a ├║nica pista estava na mensagem interna.
                resultados.Add(new
                {
                    provedor = nome,
                    resultado = "falhou",
                    erro = ex.GetType().Name,
                    causa = ex.InnerException?.Message,
                    mensagem = ex.Message
                });
            }
        }

        return resultados;
    }

    private async Task<(decimal Latitude, decimal Longitude)?> ConsultarPhotonAsync(
        string consulta,
        string endereco,
        CancellationToken cancellationToken)
    {
        // Sem o par├ómetro lang. O Photon responde 400 para lang=pt, embora aceite
        // lang=de: a lista de idiomas dele n├úo cobre portugu├¬s, e mandar um valor
        // fora dela derruba a consulta inteira - que ├® exatamente o tipo de falha
        // que devolve 400 e se confunde com "n├úo achou o endere├ºo".
        var url = $"api/?q={HttpUtility.UrlEncode(consulta)}&limit=1";

        try
        {
            using var resposta = await _httpClient.GetAsync(url, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogError("Photon respondeu {Codigo} para {Consulta}.", (int)resposta.StatusCode, consulta);
                UltimaFalha = $"photon respondeu HTTP {(int)resposta.StatusCode}";

                return null;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            using var json = JsonDocument.Parse(corpo);

            if (json.RootElement.TryGetProperty("features", out var features) &&
                features.ValueKind == JsonValueKind.Array &&
                features.GetArrayLength() > 0)
            {
                var coordenadas = features[0].GetProperty("geometry").GetProperty("coordinates");

                // GeoJSON vem em longitude, latitude - nessa ordem. Invertido,
                // o ponto cai no hemisferio errado e a distancia sai absurda.
                if (LerCoordenada(coordenadas, 1) is { } lat && LerCoordenada(coordenadas, 0) is { } lon)
                {
                    return (lat, lon);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "Photon falhou para {Consulta}.", consulta);
            UltimaFalha = $"{ex.GetType().Name} no photon: {ex.InnerException?.Message ?? ex.Message}";

            return null;
        }
    }

    private async Task<(decimal Latitude, decimal Longitude)?> ConsultarArcGisAsync(
        string cep,
        string endereco,
        CancellationToken cancellationToken)
    {
        var url = "https://geocode.arcgis.com/arcgis/rest/services/World/GeocodeServer/findAddressCandidates" +
                  $"?SingleLine={HttpUtility.UrlEncode(cep)}" +
                  "&f=json&maxLocations=1&outFields=*";

        try
        {
            using var resposta = await _httpClient.GetAsync(url, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogError("ArcGIS respondeu {Codigo} para o CEP {Cep}.", (int)resposta.StatusCode, cep);
                UltimaFalha = $"arcgis respondeu HTTP {(int)resposta.StatusCode}";

                return null;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            using var json = JsonDocument.Parse(corpo);

            if (json.RootElement.TryGetProperty("candidates", out var candidatos) &&
                candidatos.ValueKind == JsonValueKind.Array &&
                candidatos.GetArrayLength() > 0)
            {
                var local = candidatos[0].GetProperty("location");

                // ArcGIS tambem vem em x = longitude, y = latitude.
                if (local.TryGetProperty("y", out var y) && local.TryGetProperty("x", out var x))
                {
                    if (LerCoordenada(y, 0) is { } lat && LerCoordenada(x, 0) is { } lon)
                    {
                        return (lat, lon);
                    }
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "ArcGIS falhou para o CEP {Cep}.", cep);
            UltimaFalha = $"{ex.GetType().Name} no arcgis: {ex.InnerException?.Message ?? ex.Message}";

            return null;
        }
    }

    /// <summary>
    /// L├¬ um n├║mero do GeoJSON, que o Photon devolve como texto e o ArcGIS
    /// como n├║mero.
    ///
    /// Os dois aparecem de verdade em produ├º├úo: assumir s├│ um dos formatos
    /// derruba a leitura da coordenada, e o c├ílculo passa a recusar todo CEP.
    /// </summary>
    private static decimal? LerCoordenada(JsonElement elemento, int indice)
    {
        var alvo = elemento[indice];

        if (alvo.ValueKind == JsonValueKind.Number)
        {
            return alvo.TryGetDecimal(out var numero) ? numero : null;
        }

        if (alvo.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(
                alvo.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var texto) ? texto : null;
        }

        return null;
    }

    private static string? ExtrairCep(string endereco)
    {
        var digitos = new string((endereco ?? string.Empty).Where(char.IsDigit).ToArray());

        // O CEP brasileiro tem oito d├¡gitos. A tela escreve 18072-856, que s├úo
        // sete, e o tra├ºo do CEP ├® separador: sem completar com zero, o CEP
        // era descartado e a busca sa├¡a com o endere├ºo inteiro.
        if (digitos.Length < 5)
        {
            return null;
        }

        var primeiros = digitos[..5];
        var resto = digitos[5..].PadRight(3, '0')[..3];

        return $"{primeiros}-{resto}";
    }
}
