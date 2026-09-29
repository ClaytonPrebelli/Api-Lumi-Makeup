namespace LumiMakeup.Domain.Entities;

/// <summary>
/// Cupom de desconto percentual.
///
/// A quantidade é um **estoque de usos**, não um limite de uma vez por cliente: cada
/// pedido que usa o cupom consome uma unidade. Quando chega a zero, o código deixa
/// de valer — e volta a valer se a quantidade for aumentada de novo. Não há
/// reposição automática, e é esse contador que o painel administra.
///
/// O desconto incide sobre o subtotal dos **produtos**, nunca sobre o frete.
/// Cupom sobre frete subsidia o transporte em vez da mercadoria, que é o contrário do
/// que uma promoção pretende.
/// </summary>
public class Cupom
{
    public long Id { get; set; }

    /// <summary>O texto que o cliente digita. Único, e sem acento nem caixa.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Percentual de desconto, de 0 a 100.</summary>
    public decimal Percentual { get; set; }

    /// <summary>Usos restantes. Zero significa que o cupom não vale mais.</summary>
    public int QuantidadeDisponivel { get; set; }

    /// <summary>Subtotal mínimo de produtos para o cupom ser aceito.</summary>
    public decimal ValorMinimo { get; set; }

    /// <summary>
    /// Data de término, comparada contra a data do pedido. Nulo significa que não
    /// expira.
    /// </summary>
    public DateTime? ValidadeAte { get; set; }

    /// <summary>Chave de liga e desliga. Desligado, o cupom não vale.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
