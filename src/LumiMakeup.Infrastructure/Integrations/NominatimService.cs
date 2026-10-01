using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Geocodificacao de endereco, para o frete saber a distancia entre a loja e
/// o CEP do cliente.
///
/// A consulta e' ESTRUTURADA, e nao texto livre. Texto livre com o mesmo
/// endereco as vezes voltava vazio: o Nominatim e' exigeente com o formato, e
/// duas variantes do mesmo logradouro davam resultado numa e vazio na outra -
/// sem qualquer aviso, porque a resposta de erro dele e' HTTP 200 com [].
///
/// Nos campos estruturados cada parte vai no seu parametro, e o servico nao
/// precisa adivinhar o que ele considera valido.
/// </summary>
public sealed class NominatimService : INominatimService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimService> _logger;

    public NominatimService(HttpClient httpClient, ILogger<NominatimService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string? UltimoFalha { get; private set; }

    /// <summary>
    /// O mesmo cliente que o geocodificador usa, para testar a saída do servidor.
    ///
    /// Não é o HttpClient do IHttpClientFactory de propósito: o pool daquele é
    /// configurado para o caso comum e pode estar registrado em outro lugar. O
    /// diagnóstico tem que exercitar exatamente o cliente que falha no cálculo,
    /// senão ele testa uma coisa e o frete usa outra.
    /// </summary>
    public HttpClient CriarClienteDeDiagnostico() => _httpClient;

    public async Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default)
    {
        var partes = SepararEndereco(endereco);

        // Zera o motivo da consulta anterior: sem isso, uma CEP que falhou por
        // rede ensinaria a proxima, que deu certo, que ainda estava falhando.
        UltimoFalha = null;

        if (partes is null || partes.Cep is null)
        {
            // A consulta e' pelo CEP, e so ele. Exigir cidade aqui descartava
            // justamente as consultas que trazem o CEP sozinho - que sao as que
            // o calculo e o diagnostico montam - e o resultado era "sem
            // coordenada" sem chegar a falar com o mapa.
            //
            // A cidade nao e necessaria: quem decide o ponto devolvido e' o CEP.
            // E o plano B com a cidade, que existia antes, e' o que traria o
            // centro da cidade, que nao e o endereco do cliente.
            _logger.LogWarning(
                "Endereco sem CEP identificavel, impossivel geocodificar: {Endereco}",
                endereco);

            UltimoFalha = $"endereco sem CEP: '{endereco}'";

            return null;
        }

        // A consulta e' pelo CEP, e o CEP manda no ponto devolvido.
        //
        // Somando a cidade a consulta, o servico passa a devolver o CENTRO da
        // cidade, do tipo "administrative". Os dois CEPs caem no mesmo ponto, a
        // distancia sai zero, e todo cliente da mesma cidade paga a taxa minima
        // como se morasse ao lado. Em cidade grande isso erra o frete de uma
        // ponta a outra.
        //
        // Nao ha plano B com a cidade: se o mapa nao tem o CEP, e melhor recusar
        // do que inventar uma posicao. O centro da cidade nao e o endereco do
        // cliente, e cobrar por ele seria cobrar por um lugar em que ele nao esta.
        var peloCep = await ConsultarAsync(
            $"postalcode={HttpUtility.UrlEncode(partes.Cep ?? string.Empty)}",
            endereco,
            cancellationToken);

        if (peloCep is not null)
        {
            return peloCep;
        }

        // O motivo fica aqui apenas quando a consulta chegou ao mapa e ele
        // respondeu que nao tem o ponto. Se a consulta falhou - 403, 429,
        // timeout - o motivo ja foi registrado em ConsultarAsync, e sobrescrever
        // aqui faria o diagnostico culpar o mapa por um problema de rede.
        if (UltimoFalha is null)
        {
            UltimoFalha = $"o mapa nao tem ponto para o CEP {partes.Cep}";
        }

        // Sem ponto para o CEP, a resposta honesta e nao ter coordenada. O
        // chamador transforma isso em "nao conseguimos localizar esse
        // endereco", e a administradora ve que precisa ligar para a loja - em
        // vez de receber um frete inventado a partir do centro da cidade.
        _logger.LogWarning(
            "CEP {Cep} sem ponto proprio no OpenStreetMap; sem coordenada para o calculo.",
            partes.Cep);

        return null;
    }

    private async Task<(decimal Latitude, decimal Longitude)?> ConsultarAsync(
        string parametros,
        string endereco,
        CancellationToken cancellationToken)
    {
        var url = $"search?{parametros}&format=json&limit=1";

        try
        {
            using var resposta = await _httpClient.GetAsync(url, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
            {
                // A URL vai no log de proposito: quando o calculo recusa, a
                // diferenca entre "o mapa nao tem esse CEP" e "o servidor nao
                // conseguiu falar com o mapa" so aparece olhando a chamada.
                // 403 e bloqueio por User-Agent, 429 e excesso de requisicao, e
                // os dois precisam de correcao diferente de quem le o log.
                _logger.LogError(
                    "Nominatim respondeu {Codigo} para {Url}.",
                    (int)resposta.StatusCode,
                    url);

                UltimoFalha = $"o mapa respondeu HTTP {(int)resposta.StatusCode} para {url}";

                return null;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            using var json = JsonDocument.Parse(corpo);

            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
            {
                _logger.LogWarning("Nominatim nao encontrou nada para {Url}.", url);

                UltimoFalha = $"o mapa nao devolveu nada para {url}";

                return null;
            }

            var primeiro = json.RootElement[0];

            if (!primeiro.TryGetProperty("lat", out var latitude) ||
                !primeiro.TryGetProperty("lon", out var longitude) ||
                !decimal.TryParse(latitude.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat) ||
                !decimal.TryParse(longitude.GetString(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                _logger.LogWarning("Nominatim devolveu o endereco sem coordenadas utilizaveis: {Endereco}", endereco);

                return null;
            }

            return (lat, lon);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // A URL entra aqui porque a excecao sozinha nao diz o bastante: um
            // timeout e um DNS falhando sao o mesmo "Nominatim nao respondeu", e
            // a diferenca aparece so na chamada.
            _logger.LogError(ex, "Falha ao geocodificar {Url} para o endereco {Endereco}.", url, endereco);

            UltimoFalha = $"{ex.GetType().Name} em {url}: {ex.Message}";

            // A falha mais comum aqui nao e' do mapa, e' do servidor: o Windows
            // Server nao consegue fechar a conexao TLS com o Nominatim, e o
            // erro chega embrulhado em "The SSL connection could not be
            // established". Sem isto, a causa real fica enterrada em
            // HttpRequestException e so aparece se alguem abrir o log.
            if (ex is HttpRequestException { InnerException: System.Security.Authentication.AuthenticationException })
            {
                _logger.LogError(
                    "Falha de TLS com o Nominatim. A saida HTTPS do servidor funciona " +
                    "(ViaCEP e Google respondem), entao o problema e' a negociacao com " +
                    "esse host. Verifique se TLS 1.2 esta habilitado no Windows " +
                    "(SchUseStrongCrypto) e reinicie o servidor. Endereco: {Url}.",
                    url);
            }

            return null;
        }
    }

    /// <summary>
    /// Separa o endereco que o chamador monta em suas partes.
    ///
    /// O chamador manda uma unica string porque e o que o servico de CEP ja
    /// devolve. Aqui ela vira campo, e cada campo vai no parametro certo do
    /// Nominatim - que e o que evita a busca por texto livre.
    ///
    /// O CEP vem entre aspas no formato antigo, e as aspas faziam a busca
    /// devolver vazio. Por isso elas sao removidas aqui.
    /// </summary>
    private static PartesDoEndereco? SepararEndereco(string endereco)
    {
        var semAspas = endereco
            .Replace("\"", string.Empty)
            // A cidade vem como "Sao Paulo - SP" em uns enderecos e como
            // "Sao Paulo, SP" em outros. Sem normalizar o hifen, a sigla do
            // estado nao era reconhecida e a cidade acabava com o estado grudado:
            // "Sao Paulo - SP" virava o nome de uma cidade que nao existe.
            .Replace(" - ", ",");

        // O CEP e' o primeiro pedaco, porem as aspas as vezes o fazem vir
        // grudado ao logradouro ("15000-000, Alameda Santos" ja vem separado,
        // mas "15000-000Alameda Santos" nao). Separar por virgula primeiro e so
        // depois tirar o CEP do inicio do primeiro pedacoresolve os dois casos.
        var pedacos = semAspas
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        string? cep = null;
        string? estado = null;

        // Estado e' a sigla de duas letras, e vem no fim.
        var ultimo = pedacos.LastOrDefault();

        if (ultimo is not null && ultimo.Length == 2 && ultimo.All(char.IsLetter))
        {
            estado = ultimo.ToUpperInvariant();
        }

        foreach (var pedaco in pedacos)
        {
            var digitos = new string(pedaco.Where(char.IsDigit).ToArray());

            // CEP brasileiro tem oito dígitos, escritos como 5-3. A tela digita
            // 18072-856, que são sete, e o traço do CEP conta como separador: sem
            // esta normalização, o CEP era recusado como "endereço sem CEP" e o
            // cálculo nunca chegava a consultar o mapa.
            if (digitos.Length == 8)
            {
                digitos = digitos[..8];
            }
            else if (digitos.Length == 7)
            {
                digitos = $"{digitos[..5]}0{digitos[5..]}";
            }
            else if (digitos.Length == 6)
            {
                digitos = $"{digitos[..5]}00{digitos[5..]}";
            }

            if (digitos.Length == 8 && cep is null)
            {
                cep = $"{digitos[..5]}-{digitos[5..8]}";
            }
        }

        // A cidade e' o pedaco imediatamente antes do estado, ignorando o CEP.
        //
        // Escolher pelo "ultimo antes do estado" evita adivinhar, mas o CEP
        // precisa sair da lista antes: ele vem no inicio, e sem remove-lo o
        // "ultimo antes do estado" seria o bairro, e o resultado seria um lugar
        // errado - com o frete calculado em cima.
        var candidatos = pedacos
            .Where(p => p != estado)
            .Where(p => new string(p.Where(char.IsDigit).ToArray()).Length < 7)
            .ToArray();

        string? cidade = estado is not null && candidatos.Length >= 2
            ? candidatos[^1]
            : null;

        // A cidade nao e obrigatoria. A consulta e' pelo CEP, e sem o CEP nao ha
        // o que consultar - mas com o CEP ja da, mesmo que o address nao traga
        // cidade.
        //
        // Antes estarecava aqui, e era por isso que uma consulta so com o CEP
        // voltava "sem coordenada" sem nunca falar com o mapa.
        return new PartesDoEndereco(cep, cidade, estado);
    }

    private sealed record PartesDoEndereco(string? Cep, string? Cidade, string? Estado);
}
