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

    public Task<bool> SendMessageAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "WhatsAppServiceStub: mensagem NÃO enviada (microserviço Baileys pendente). Para={PhoneNumber}",
            phoneNumber);
        return Task.FromResult(false);
    }
}