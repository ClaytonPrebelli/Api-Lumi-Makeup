namespace LumiMakeup.Domain.Entities;

public class ImagemProduto
{
    public long Id { get; set; }
    public long ProdutoId { get; set; }
    public string CaminhoRelativo { get; set; } = string.Empty;
    public string NomeOriginal { get; set; } = string.Empty;
    public int Ordem { get; set; }

    public Produto Produto { get; set; } = null!;
}
