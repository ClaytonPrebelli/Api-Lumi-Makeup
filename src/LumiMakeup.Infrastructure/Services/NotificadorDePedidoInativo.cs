using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Services;

/// <summary>
/// Um notificador que não avisa nada, e por isso avisa no log.
///
/// Existe só para o <see cref="GestaoDePedidosService"/> poder existir antes do
/// envio de verdade. Registrar aviso em vez de ficar quieto é o ponto: silencioso
/// aqui pareceria "a notificação foi enviada" e o fluxo pareceria pronto, quando o
/// cliente ainda não recebe nada.
///
/// Sai de cena quando o notificador real — e-mail ao cliente, e-mail à
/// administradora e WhatsApp do número dela — entrar no lugar.
/// </summary>
public sealed class NotificadorDePedidoInativo : INotificadorDePedido
{
    private readonly ILogger<NotificadorDePedidoInativo> _logger;

    public NotificadorDePedidoInativo(ILogger<NotificadorDePedidoInativo> logger)
    {
        _logger = logger;
    }

    public Task PedidoCriadoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Pedido {PedidoId} criado, mas o notificador ainda nao envia: e-mail e WhatsApp estao pendentes.",
            pedido.Id);

        return Task.CompletedTask;
    }

    public Task PedidoPagoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Pedido {PedidoId} pago, sem aviso enviado ao cliente.", pedido.Id);
        return Task.CompletedTask;
    }

    public Task PedidoCanceladoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Pedido {PedidoId} cancelado, sem aviso enviado ao cliente.", pedido.Id);
        return Task.CompletedTask;
    }
}
