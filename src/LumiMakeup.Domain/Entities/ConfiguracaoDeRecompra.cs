namespace LumiMakeup.Domain.Entities;

public class ConfiguracaoDeRecompra
{
    public long Id { get; set; }
    public bool Ativa { get; set; }
    public string Assunto { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public DateTime AtualizadoEm { get; set; }
}
