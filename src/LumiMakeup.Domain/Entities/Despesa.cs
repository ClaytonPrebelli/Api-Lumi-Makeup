namespace LumiMakeup.Domain.Entities;

public class Despesa
{
    public long Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataDaDespesa { get; set; } = DateTime.UtcNow;
    public long CriadoPor { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public Usuario CriadoPorUsuario { get; set; } = null!;
}