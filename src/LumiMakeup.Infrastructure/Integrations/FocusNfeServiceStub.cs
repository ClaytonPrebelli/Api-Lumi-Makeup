using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class FocusNfeServiceStub : IFocusNfeService
{
    private readonly ILogger<FocusNfeServiceStub> _logger;

    public FocusNfeServiceStub(ILogger<FocusNfeServiceStub> logger)
    {
        _logger = logger;
    }

    public Task<string> EmitInvoiceAsync(long orderId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("FocusNfeServiceStub: emissão de nota fiscal pendente (fase 2). OrderId={OrderId}", orderId);
        return Task.FromResult(string.Empty);
    }
}