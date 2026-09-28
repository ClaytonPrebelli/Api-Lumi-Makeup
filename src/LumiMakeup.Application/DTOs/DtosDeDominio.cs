using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record CategoriaDto(long Id, string Nome, string Slug, string? Descricao, bool Ativo);

public sealed record ImagemProdutoDto(long Id, string CaminhoRelativo, string NomeOriginal, int Ordem);

public sealed record ProdutoAdministracaoDto(
    long Id,
    string Nome,
    string Slug,
    string Descricao,
    decimal PrecoCusto,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque,
    DateTime CriadoEm,
    long CategoriaId,
    string NomeCategoria,
    IReadOnlyList<ImagemProdutoDto> Imagens);

public sealed record RequisicaoDeProduto(
    long CategoriaId,
    string Nome,
    string? Slug,
    string Descricao,
    decimal PrecoCusto,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque);

public sealed record RequisicaoDeOrdenacaoDeImagens(IReadOnlyList<long> Ordem);

public sealed record RequisicaoDeCategoria(string Nome, string? Slug, string? Descricao, bool Ativo);

public sealed record RequisicaoDeMelhoriaDeTexto(string? Nome, string Descricao);

public sealed record RespostaDeMelhoriaDeTextoDto(string DescricaoMelhorada, string ModeloUsado);

/// <summary>
/// Produto na vitrine. Não leva <c>PrecoCusto</c>: custo é informação interna
/// e deixá-lo na API pública entregaria a margem de quem compra.
/// </summary>
public sealed record ProdutoDto(
    long Id,
    string Nome,
    string Slug,
    string Descricao,
    decimal PrecoVenda,
    decimal? PrecoPromocional,
    int QuantidadeEstoque,
    bool Ativo,
    bool Destaque,
    long CategoriaId,
    string NomeCategoria,
    IReadOnlyList<ImagemProdutoDto> Imagens);

public sealed record PedidoDto(
    long Id,
    StatusPedido Status,
    StatusEntrega StatusEntrega,
    MetodoPagamento? MetodoPagamento,
    decimal Subtotal,
    decimal CustoFrete,
    decimal Total,
    DateTime CriadoEm,
    IReadOnlyList<ItemPedidoDto> Itens);

public sealed record ItemPedidoDto(
    long Id,
    long ProdutoId,
    string NomeProduto,
    decimal PrecoVendaUnitario,
    int Quantidade,
    decimal Subtotal);