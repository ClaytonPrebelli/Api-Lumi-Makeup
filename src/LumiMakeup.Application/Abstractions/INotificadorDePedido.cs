using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

/// <summary>
/// Avisa que o pedido foi criado: e-mail ao cliente, e-mail à administradora e
/// WhatsApp ao cliente, do número dela.
///
/// Está atrás de uma interface porque quem chama é o pedido, e quem manda a
/// mensagem é outro assunto. Sem essa separação, o fluxo do checkout conheceria o
/// SMTP e o Baileys.
///
/// <para>
/// Nada aqui pode derrubar o pedido. A implementação registra a falha e segue: um
/// e-mail que não sai não desfaz uma compra já gravada, e o painel consegue
/// reenviar. O contrário — desfazer a compra porque a mensagem falhou — deixaria o
/// cliente sem pedido e sem aviso.
/// </para>
/// </summary>
public interface INotificadorDePedido
{
    Task PedidoCriadoAsync(PedidoDto pedido, CancellationToken cancellationToken = default);

    /// <summary>Avisa que a administradora registrou o pagamento e o pedido virou pago.</summary>
    Task PedidoPagoAsync(PedidoDto pedido, CancellationToken cancellationToken = default);

    /// <summary>Avisa que o pedido foi cancelado.</summary>
    Task PedidoCanceladoAsync(PedidoDto pedido, CancellationToken cancellationToken = default);
}
