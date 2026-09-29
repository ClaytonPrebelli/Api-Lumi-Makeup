using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class WhatsAppServiceStub : IWhatsAppService
{
    private readonly ILogger<WhatsAppServiceStub> _logger;

    public WhatsAppServiceStub(ILogger<WhatsAppServiceStub> logger)
    {
        _logger = logger;
    }

    public Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "WhatsAppServiceStub: mensagem NǟO enviada (microservi��o Baileys pendente). Para={Telefone}",
            telefone);
        return Task.FromResult(false);
    }

    /// <summary>
    /// Diz "fora do ar" em vez de "pareado, mas nao conectado". O painel
    /// precisa conseguir distinguir os dois: sem o Node, o primeiro passo e
    /// instalar, e nao escanear QR.
    /// </summary>
    public Task<StatusDoWhatsApp> ObterStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new StatusDoWhatsApp(false, false, null, null, null, null));

    /// <summary>
    /// Sem Node nao ha QR. A tela mostra que o servico esta desligado, em vez
    /// de ficar esperando um QR que nunca chega.
    /// </summary>
    public Task<string?> ObterQrDePareamentoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}