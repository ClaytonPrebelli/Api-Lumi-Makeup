using LumiMakeup.Dominio.Enums;

namespace LumiMakeup.Dominio.Entidades;

public class NotaFiscal
{
    public long Id { get; set; }
    public long PedidoId { get; set; }
    public StatusNotaFiscal Status { get; set; } = StatusNotaFiscal.Pendente;
    public string? ReferenciaFocusNfe { get; set; }
    public string? UrlXml { get; set; }
    public string? UrlPdf { get; set; }
    public DateTime? EmitidaEm { get; set; }

    public Pedido Pedido { get; set; } = null!;
}