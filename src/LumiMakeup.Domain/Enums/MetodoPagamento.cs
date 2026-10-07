namespace LumiMakeup.Domain.Enums;

/// <summary>
/// <c>Cartao</c> é legado: vendas antigas no cartão sem separar débito de
/// crédito. Continua no enum para as linhas antigas materializarem; telas novas
/// oferecem <c>CartaoDebito</c> e <c>CartaoCredito</c>.
/// </summary>
public enum MetodoPagamento
{
    Pix,
    Cartao,
    Dinheiro,
    Outro,
    CartaoDebito,
    CartaoCredito
}