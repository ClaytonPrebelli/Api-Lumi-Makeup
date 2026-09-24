using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class ServicoFocusNfeStub : IServicoFocusNfe
{
    private readonly ILogger<ServicoFocusNfeStub> _logger;

    public ServicoFocusNfeStub(ILogger<ServicoFocusNfeStub> logger)
    {
        _logger = logger;
    }

    public Task<string> EmitirNotaAsync(long pedidoId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ServicoFocusNfeStub: emissão de nota fiscal pendente (fase 2). PedidoId={PedidoId}", pedidoId);
        return Task.FromResult(string.Empty);
    }
}