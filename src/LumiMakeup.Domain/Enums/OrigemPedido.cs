namespace LumiMakeup.Domain.Enums;

/// <summary>
/// De onde veio o pedido.
///
/// Sem este campo a venda de balcão entra misturada na receita do site, e nenhum
/// relatório separa o que foi e-commerce do que foi venda presencial. Num dado
/// perguntas, é a diferença entre "a loja faturou X" e "a loja vendeu X pela internet".
/// </summary>
public enum OrigemPedido
{
    Online = 0,
    Balcao = 1
}
