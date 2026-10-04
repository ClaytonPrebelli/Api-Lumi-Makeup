namespace LumiMakeup.Domain.Entities;

public class VarianteProduto
{
    public long Id { get; set; }
    public long ProdutoId { get; set; }
    public string Nome { get; set; } = string.Empty; // Ex: "Vermelho", "Rosa"
    public string CorHex { get; set; } = string.Empty; // Ex: "#FF0000"
    public int QuantidadeEstoque { get; set; }
    public decimal? PrecoAdicional { get; set; } // Preço extra sobre o preço base do produto (opcional)
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; } // Para ordenação no frontend
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Produto Produto { get; set; } = null!;
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
}