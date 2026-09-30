using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class BaileysWhatsAppService : IWhatsAppService
{
    private static readonly JsonSerializerOptions OpcoesDeJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly OpcoesDeBaileys _opcoes;
    private readonly ILogger<BaileysWhatsAppService> _logger;
    private readonly EstadoDoNodeBaileys? _estado;

    public BaileysWhatsAppService(
        HttpClient http,
        IOptions<OpcoesDeBaileys> opcoes,
        ILogger<BaileysWhatsAppService> logger,
        EstadoDoNodeBaileys? estado = null)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _logger = logger;
        _estado = estado;
    }

    /// <summary>
    /// Quando o Node nao responde, o motivo vem do supervisor.
    ///
    /// O supervisor e quem sabe se a pasta existe, se o executavel foi
    /// encontrado e se o processo subiu. A tela precisa disso: "o servico esta
    /// parado" sem causa nao ajuda ninguem a agir, e no servidor de producao
    /// nao ha console para olhar o log.
    /// </summary>
    private StatusDoWhatsApp Parado() =>
        new(
            ServicoNoAr: false,
            Pareado: false,
            Numero: null,
            Nome: null,
            ConectadoDesde: null,
            UltimoEnvioEm: null,
            Motivo: _estado?.Explicacao());

    /// <summary>
    /// Envia texto pelo numero pareado do Node do Baileys.
    ///
    /// Devolve <c>false</c> em vez de lancar excecao. O chamador e o
    /// <c>NotificadorDePedido</c>, que ja isola o aviso de WhatsApp em try
    /// proprio: a falha e registrada e o pedido segue. Lancar aqui transformaria
    /// "numero nao pareado" em excecao de regra de negocio.
    /// </summary>
    public async Task<bool> EnviarMensagemAsync(
        string telefone,
        string mensagem,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.SegredoCompartilhado))
        {
            _logger.LogError(
                "WhatsApp nao enviado para {Telefone}: ExternalServices:Baileys:SegredoCompartilhado esta vazio.",
                telefone);

            return false;
        }

        try
        {
            using var requisicao = new HttpRequestMessage(HttpMethod.Post, "enviar")
            {
                Content = JsonContent.Create(
                    new RequisicaoDeEnvio(telefone, mensagem),
                    options: OpcoesDeJson)
            };

            requisicao.Headers.Add("x-segredo", _opcoes.SegredoCompartilhado);

            using var resposta = await _http.SendAsync(requisicao, cancellationToken);

            if (resposta.IsSuccessStatusCode)
            {
                return true;
            }

            // 503 e o Node dizendo que o numero ainda nao esta pareado. E o caso
            // mais comum depois de um restart, e o log precisa dizer isso sem
            // parecer falha de codigo.
            if (resposta.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                _logger.LogWarning(
                    "WhatsApp nao enviado para {Telefone}: o Node esta no ar, mas o numero ainda nao foi pareado com o QR.",
                    telefone);

                return false;
            }

            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogError(
                "WhatsApp nao enviado para {Telefone}: o Node respondeu {(int)resposta.StatusCode} {Corpo}",
                telefone,
                (int)resposta.StatusCode,
                corpo);

            return false;
        }
        catch (HttpRequestException excecao)
        {
            // Servico fora do ar e o caso mais comum em producao: o Node morreu,
            // ou nunca subiu. A mensagem aqui e o que faz a administradora
            // entender o sintoma sem abrir o servidor.
            _logger.LogError(
                excecao,
                "WhatsApp nao enviado para {Telefone}: nao consegui falar com o Node do Baileys em {UrlBase}.",
                telefone,
                _opcoes.UrlBase);

            return false;
        }
        catch (TaskCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                excecao,
                "WhatsApp nao enviado para {Telefone}: o Node do Baileys nao respondeu a tempo.",
                telefone);

            return false;
        }
    }

    public async Task<StatusDoWhatsApp> ObterStatusAsync(CancellationToken cancellationToken = default)
    {
        var (resposta, _) = await ConsultarAsync("status", cancellationToken);

        if (resposta is null)
        {
            // Node fora do ar nao e excecao: e o estado atual, e o painel precisa
            // mostrar "servico parado" em vez de uma tela de erro.
            return Parado();
        }

        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Leitura do status do Baileys falhou: o Node respondeu {(int)resposta.StatusCode}.",
                (int)resposta.StatusCode);

            return new StatusDoWhatsApp(true, false, null, null, null, null, null);
        }

        var corpo = await LerCorpoAsync(resposta, cancellationToken);

        if (corpo is null)
        {
            return new StatusDoWhatsApp(true, false, null, null, null, null, null);
        }

        try
        {
            using var json = JsonDocument.Parse(corpo);
            var raiz = json.RootElement;

            return new StatusDoWhatsApp(
                ServicoNoAr: true,
                Pareado: LerBooleano(raiz, "pareado"),
                Numero: LerTexto(raiz, "numero"),
                Nome: LerTexto(raiz, "nome"),
                ConectadoDesde: LerData(raiz, "inicioEm"),
                UltimoEnvioEm: LerData(raiz, "ultimoEnvioEm"),
                Motivo: null);
        }
        catch (JsonException excecao)
        {
            _logger.LogError(excecao, "O Node do Baileys devolveu um /status fora do formato.");

            return new StatusDoWhatsApp(true, false, null, null, null, null, null);
        }
    }

    public async Task<string?> ObterQrDePareamentoAsync(CancellationToken cancellationToken = default)
    {
        var (resposta, _) = await ConsultarAsync("pareamento", cancellationToken);

        if (resposta is null)
        {
            _logger.LogWarning("QR de pareamento indisponivel: o Node do Baileys nao esta no ar.");

            return null;
        }

        // 409 e o Node avisando que nao ha QR: ou ja esta pareado, ou o numero
        // foi bloqueado pelo WhatsApp. Nao e falha, e a tela precisa mostrar o
        // aviso em vez de tentar desenhar um QR inexistente.
        if (resposta.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }

        if (!resposta.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Leitura do QR de pareamento falhou: o Node respondeu {(int)resposta.StatusCode}.",
                (int)resposta.StatusCode);

            return null;
        }

        var corpo = await LerCorpoAsync(resposta, cancellationToken);

        if (corpo is null)
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(corpo);
            var qr = json.RootElement.TryGetProperty("qr", out var elemento) ? elemento.GetString() : null;

            return string.IsNullOrWhiteSpace(qr) ? null : qr;
        }
        catch (JsonException excecao)
        {
            _logger.LogError(excecao, "O Node do Baileys devolveu um /pareamento fora do formato.");

            return null;
        }
    }

    public async Task<string?> ForcarReconexaoDePareamentoAsync(CancellationToken cancellationToken = default)
    {
        var (resposta, _) = await ConsultarAsync("pareamento/reconectar", cancellationToken, HttpMethod.Post);

        if (resposta is null)
        {
            _logger.LogWarning("Reconeccao forcada indisponivel: o Node do Baileys nao esta no ar.");

            return null;
        }

        // 409 e o Node avisando que ele respeitou a trava de intervalo: o
        // clique foi cedo demais. Nao e falha, e a tela mostra a espera.
        if (!resposta.IsSuccessStatusCode && resposta.StatusCode != HttpStatusCode.Conflict)
        {
            _logger.LogError(
                "Reconeccao forcada falhou: o Node respondeu {(int)resposta.StatusCode}.",
                (int)resposta.StatusCode);

            return null;
        }

        var corpo = await LerCorpoAsync(resposta, cancellationToken);

        if (corpo is null)
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(corpo);

            return LerTexto(json.RootElement, "qr");
        }
        catch (JsonException excecao)
        {
            _logger.LogError(excecao, "O Node devolveu uma reconeccao fora do formato.");

            return null;
        }
    }

    /// <summary>
    /// Chamada de leitura, com o segredo no cabecalho.
    ///
    /// Devolve o par resposta + booleano "respondeu". O booleano existe para
    /// distinguir "o Node disse que nao ha QR" de "o Node nao atendeu": as duas
    /// viram <c>null</c> para quem chama, mas a segunda precisa de log.
    /// </summary>
    private async Task<(HttpResponseMessage? Resposta, bool Respondeu)> ConsultarAsync(
        string caminho,
        CancellationToken cancellationToken,
        HttpMethod metodo = null!)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.SegredoCompartilhado))
        {
            _logger.LogError(
                "Consulta a {Caminho} recusada: ExternalServices:Baileys:SegredoCompartilhado esta vazio.",
                caminho);

            return (null, true);
        }

        try
        {
            using var requisicao = new HttpRequestMessage(metodo ?? HttpMethod.Get, caminho);
            requisicao.Headers.Add("x-segredo", _opcoes.SegredoCompartilhado);

            var resposta = await _http.SendAsync(requisicao, cancellationToken);

            return (resposta, true);
        }
        catch (HttpRequestException excecao)
        {
            _logger.LogError(
                excecao,
                "Consulta a {Caminho} falhou: nao consegui falar com o Node do Baileys em {UrlBase}.",
                caminho,
                _opcoes.UrlBase);

            return (null, false);
        }
        catch (TaskCanceledException excecao) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(excecao, "Consulta a {Caminho} falhou: o Node do Baileys nao respondeu a tempo.", caminho);

            return (null, false);
        }
    }

    private static async Task<string?> LerCorpoAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(corpo) ? null : corpo;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static bool LerBooleano(JsonElement raiz, string propriedade) =>
        raiz.TryGetProperty(propriedade, out var elemento) &&
        elemento.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        elemento.GetBoolean();

    private static string? LerTexto(JsonElement raiz, string propriedade) =>
        raiz.TryGetProperty(propriedade, out var elemento) && elemento.ValueKind == JsonValueKind.String
            ? elemento.GetString()
            : null;

    /// <summary>
    /// O Node manda ISO 8601 com Z, que .NET entende direto. Data invalida
    /// vira null em vez de estourar: uma data de "conectado desde" quebrada
    /// nao pode derrubar a tela de status do painel.
    /// </summary>
    private static DateTime? LerData(JsonElement raiz, string propriedade)
    {
        var texto = LerTexto(raiz, propriedade);

        return DateTime.TryParse(
            texto,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var data)
            ? data
            : null;
    }

    private sealed record RequisicaoDeEnvio(string Telefone, string Mensagem);
}