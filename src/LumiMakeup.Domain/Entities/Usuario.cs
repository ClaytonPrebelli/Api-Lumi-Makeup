using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Usuario
{
    public long Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Nulo só para entregador, que entra com usuário e senha. Cliente sempre
    /// tem, porque conta, pedido e aviso por e-mail dependem dele.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Apelido de login do entregador, único. Nulo para cliente e admin, que
    /// entram com e-mail.
    /// </summary>
    public string? Login { get; set; }

    /// <summary>
    /// Desligar em vez de excluir: o histórico de entregas guarda o nome, e
    /// desativado não entra mais.
    /// </summary>
    public bool Ativo { get; set; } = true;

    public string? HashSenha { get; set; }
    public string? IdGoogle { get; set; }
    public string? Cpf { get; set; }
    public string? Telefone { get; set; }
    public PapelUsuario Papel { get; set; } = PapelUsuario.Cliente;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Endereco> Enderecos { get; set; } = new List<Endereco>();
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<Despesa> Despesas { get; set; } = new List<Despesa>();
    public ICollection<RecuperacaoDeSenha> RecuperacoesDeSenha { get; set; } = new List<RecuperacaoDeSenha>();
}