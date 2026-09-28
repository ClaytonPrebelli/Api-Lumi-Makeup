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
    int QuantidadeEstoque,
    bool Ativo,
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
    int QuantidadeEstoque,
    bool Ativo);

public sealed record RequisicaoDeOrdenacaoDeImagens(IReadOnlyList<long> Ordem);

public sealed record RequisicaoDeCategoria(string Nome, string? Slug, string? Descricao, bool Ativo);

public sealed record RequisicaoDeMelhoriaDeTexto(string? Nome, string Descricao);

public sealed record RespostaDeMelhoriaDeTextoDto(string DescricaoMelhorada, string ModeloUsado);

public sealed record ProdutoDto(
    long Id,
    string Nome,
    string Slug,
    string Descricao,
    decimal PrecoVenda,
    int QuantidadeEstoque,
    bool Ativo,
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