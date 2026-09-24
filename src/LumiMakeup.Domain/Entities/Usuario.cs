using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Usuario
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? HashSenha { get; set; }
    public string? IdGoogle { get; set; }
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public PapelUsuario Papel { get; set; } = PapelUsuario.Cliente;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Endereco> Enderecos { get; set; } = new List<Endereco>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<Despesa> Despesas { get; set; } = new List<Despesa>();
}