namespace LumiMakeup.Domain.Entities;

public class EnvioDeRecompra
{
    public long PedidoId { get; set; }
    public DateTime ReservadoEm { get; set; }
    public DateTime? EnviadoEm { get; set; }
    public int Versao { get; set; }
}
