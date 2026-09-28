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

    /// <summary>
    /// Preço de campanha, quando houver. `null` significa que o produto está
    /// fora de promoção e a vitrine mostra só o preço de venda.
    /// </summary>
    public decimal? PrecoPromocional { get; set; }

    public int QuantidadeEstoque { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// Marca o produto para a vitrine da home. É uma flag curada à mão, não um
    /// cálculo: a home mostra poucos produtos, e quais aparecem é uma escolha
    /// de quem monta a vitrine, não do estoque.
    /// </summary>
    public bool Destaque { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Categoria Categoria { get; set; } = null!;
    public ICollection<ImagemProduto> Imagens { get; set; } = new List<ImagemProduto>();
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
}