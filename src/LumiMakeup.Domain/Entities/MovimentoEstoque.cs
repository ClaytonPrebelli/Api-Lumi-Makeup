namespace LumiMakeup.Domain.Entities;

public enum TipoMovimentoEstoque
{
    Entrada,
    Saida,
    Ajuste
}

public class MovimentoEstoque
{
    public long Id { get; set; }
    public long ProdutoId { get; set; }
    public TipoMovimentoEstoque Tipo { get; set; }
    public int Quantidade { get; set; }
    public string? Referencia { get; set; }
    public string? Observacao { get; set; }
    public long? UsuarioId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Produto Produto { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}