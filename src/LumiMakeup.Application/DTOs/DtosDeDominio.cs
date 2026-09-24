using LumiMakeup.Domain.Enums;

namespace LumiMakeup.Application.DTOs;

public sealed record CategoriaDto(long Id, string Nome, string Slug, string? Descricao, bool Ativo);

public sealed record ImagemProdutoDto(long Id, string UrlImagem, int Ordem);

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