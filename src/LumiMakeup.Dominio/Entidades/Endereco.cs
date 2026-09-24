namespace LumiMakeup.Dominio.Entidades;

public class Endereco
{
    public long Id { get; set; }
    public long UsuarioId { get; set; }
    public string Cep { get; set; } = string.Empty;
    public string Logradouro { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool Padrao { get; set; }

    public Usuario Usuario { get; set; } = null!;
}