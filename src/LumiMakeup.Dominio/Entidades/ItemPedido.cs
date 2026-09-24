namespace LumiMakeup.Dominio.Entidades;

public class ItemPedido
{
    public long Id { get; set; }
    public long PedidoId { get; set; }
    public long ProdutoId { get; set; }
    public string NomeProdutoRegistrado { get; set; } = string.Empty;
    public decimal PrecoCustoUnitario { get; set; }
    public decimal PrecoVendaUnitario { get; set; }
    public int Quantidade { get; set; }
    public decimal Subtotal { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public Produto Produto { get; set; } = null!;
}