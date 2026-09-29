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

    public BaileysWhatsAppService(
        HttpClient http,
        IOptions<OpcoesDeBaileys> opcoes,
        ILogger<BaileysWhatsAppService> logger)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

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

    private sealed record RequisicaoDeEnvio(string Telefone, string Mensagem);
}
