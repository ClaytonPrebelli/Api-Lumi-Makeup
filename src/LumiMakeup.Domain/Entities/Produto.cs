namespace LumiMakeup.Domain.Entities;

public class Produto
{
    public long Id { get; set; }
    public long CategoriaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public decimal PrecoCusto { get; set; }
    public decimal PrecoVenda { get; set; }
    public int QuantidadeEstoque { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria Categoria { get; set; } = null!;
    public ICollection<ImagemProduto> Imagens { get; set; } = new List<ImagemProduto>();
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
}