namespace LumiMakeup.Domain.Entities;

public class RecuperacaoDeSenha
{
    public long Id { get; set; }
    public long UsuarioId { get; set; }
    public string HashToken { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime ExpiracaoEm { get; set; }
    public DateTime? UtilizadoEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
}