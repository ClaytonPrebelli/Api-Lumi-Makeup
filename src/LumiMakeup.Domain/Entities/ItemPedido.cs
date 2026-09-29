namespace LumiMakeup.Domain.Entities;

public class ItemPedido
{
    public long Id { get; set; }
    public long PedidoId { get; set; }
    public long ProdutoId { get; set; }

    /// <summary>Nome do produto no momento da compra, pelo mesmo motivo do endereço.</summary>
    public string NomeProdutoRegistrado { get; set; } = string.Empty;

    public decimal PrecoCustoUnitario { get; set; }
    public decimal PrecoVendaUnitario { get; set; }

    /// <summary>
    /// Preço promocional cobrado neste item, ou nulo se não houver promoção.
    ///
    /// Guardar os dois preços, e não só o cobrado, é o que permite o relatório
    /// responder quanto de desconto de promoção a loja concessionou, além do cupom.
    /// Com só o preço cobrado, essa conta não fecha.
    /// </summary>
    public decimal? PrecoPromocionalUnitario { get; set; }

    public int Quantidade { get; set; }

    /// <summary><c>Quantidade × (promocional ?? venda)</c>.</summary>
    public decimal Subtotal { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Produto Produto { get; set; } = null!;
}