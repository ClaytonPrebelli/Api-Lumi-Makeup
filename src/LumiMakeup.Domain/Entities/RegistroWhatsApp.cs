using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class RegistroWhatsApp
{
    public long Id { get; set; }
    public long PedidoId { get; set; }
    public string Telefone { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public StatusRegistroWhatsApp Status { get; set; }
    public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;

    public Pedido Pedido { get; set; } = null!;
}