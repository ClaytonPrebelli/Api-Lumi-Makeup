using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Domain.Entities;

public class Pedido
{
    public long Id { get; set; }

    /// <summary>
    /// Nulo quando a venda é de balcão para cliente avulso, que não tem conta.
    /// A venda de balcão com cliente cadastrado mantém o vínculo, para que o
    /// histórico de compras continue na agenda da pessoa.
    /// </summary>
    public long? UsuarioId { get; set; }

    /// <summary>
    /// Nome do cliente no momento da compra. Vai aqui, e não como referência ao
    /// cadastro, pelo mesmo motivo do endereço: o pedido guarda cópia própria do
    /// que foi comprado. Cliente trocando de nome, ou cadastro apagado, não pode
    /// reescrever o passado.
    /// </summary>
    public string NomeCliente { get; set; } = string.Empty;

    /// <summary>CPF ou documento do cliente. Nulo em venda de balcão sem documento.</summary>
    public string? DocumentoCliente { get; set; }

    public OrigemPedido Origem { get; set; } = OrigemPedido.Online;

    // O endereço é obrigatório em pedido online e nulo em venda de balcão, em que
    // o cliente não sai da loja. A regra é validada na criação do pedido, e não
    // pelo banco: uma constraint que depende de outro campo é checável em código e
    // legível na mensagem que o cliente recebe.
    public string? EnderecoCep { get; set; }
    public string? EnderecoLogradouro { get; set; }
    public string? EnderecoNumero { get; set; }
    public string? EnderecoComplemento { get; set; }
    public string? EnderecoBairro { get; set; }
    public string? EnderecoCidade { get; set; }
    public string? EnderecoEstado { get; set; }

    public StatusPedido Status { get; set; } = StatusPedido.AguardandoPagamento;
    public StatusEntrega StatusEntrega { get; set; } = StatusEntrega.NaoEnviado;
    public MetodoPagamento? MetodoPagamento { get; set; }
    public decimal DistanciaKm { get; set; }
    public decimal CustoFrete { get; set; }

    /// <summary>Soma dos itens, já com o preço promocional quando houver.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Desconto do cupom, aplicado só sobre <see cref="Subtotal"/>.</summary>
    public decimal Desconto { get; set; }

    /// <summary>Código do cupom usado, para o relatório e para auditoria.</summary>
    public string? CupomCodigo { get; set; }

    /// <summary><c>Subtotal − Desconto + Frete</c>. Nunca <c>(Subtotal + Frete) × p</c>.</summary>
    public decimal Total { get; set; }

    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? PagoEm { get; set; }
    public DateTime? EntregueEm { get; set; }

    public Usuario? Usuario { get; set; }
    public ICollection<ItemPedido> Itens { get; set; } = new List<ItemPedido>();
    public ICollection<NotaFiscal> NotasFiscais { get; set; } = new List<NotaFiscal>();
    public ICollection<RegistroWhatsApp> RegistrosWhatsApp { get; set; } = new List<RegistroWhatsApp>();
}