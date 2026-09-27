using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Pedido
{
    public long Id { get; set; }
    public long UsuarioId { get; set; }
    public string EnderecoCep { get; set; } = string.Empty;
    public string EnderecoLogradouro { get; set; } = string.Empty;
    public string EnderecoNumero { get; set; } = string.Empty;
    public string? EnderecoComplemento { get; set; }
    public string EnderecoBairro { get; set; } = string.Empty;
    public string EnderecoCidade { get; set; } = string.Empty;
    public string EnderecoEstado { get; set; } = string.Empty;
    public StatusPedido Status { get; set; } = StatusPedido.AguardandoPagamento;
    public StatusEntrega StatusEntrega { get; set; } = StatusEntrega.NaoEnviado;
    public MetodoPagamento? MetodoPagamento { get; set; }
    public decimal DistanciaKm { get; set; }
    public decimal CustoFrete { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? PagoEm { get; set; }
    public DateTime? EntregueEm { get; set; }

    public Usuario Usuario { get; set; } = null!;
    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
    public ICollection<NotaFiscal> NotasFiscais { get; set; } = new List<NotaFiscal>();
    public ICollection<RegistroWhatsApp> RegistrosWhatsApp { get; set; } = new List<RegistroWhatsApp>();
}