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

/// <summary>
/// Banner do hero visto pelo painel. Leva os dois caminhos porque o upload é feito
/// em campos separados e a tela de edicao precisa mostrar os dois arquivos.
/// </summary>
public sealed record BannerAdministracaoDto(
    long Id,
    string CaminhoRelativoDesktop,
    string CaminhoRelativoMobile,
    string NomeOriginalDesktop,
    string NomeOriginalMobile,
    string? TextoAlternativo,
    int Ordem,
    bool Ativo,
    DateTime CriadoEm);

/// <summary>
/// Banner ativo do carrossel, para a vitrine publica. Fica de fora
/// <c>NomeOriginal</c> e <c>CriadoEm</c>: sao dados do painel, e a vitrine nao
/// precisa deles.
/// </summary>
public sealed record BannerDto(
    long Id,
    string CaminhoRelativoDesktop,
    string CaminhoRelativoMobile,
    string? TextoAlternativo,
    int Ordem);

public sealed record RequisicaoDeOrdenacaoDeBanners(IReadOnlyList<long> Ordem);

public sealed record RequisicaoDeAtivacaoDeBanner(bool Ativo);

public sealed record CupomDto(
    long Id,
    string Codigo,
    decimal Percentual,
    int QuantidadeDisponivel,
    decimal ValorMinimo,
    DateTime? ValidadeAte,
    bool Ativo,
    DateTime CriadoEm);

public sealed record RequisicaoDeCupom(
    string Codigo,
    decimal Percentual,
    int QuantidadeDisponivel,
    decimal ValorMinimo,
    DateTime? ValidadeAte,
    bool Ativo);

public sealed record RequisicaoDeSomaDeQuantidadeDeCupom(int Quantidade);

/// <summary>
/// O que um cupom válido produz em um pedido: qual cupom foi, e quanto desconto
/// ele gera sobre o subtotal dos produtos.
///
/// É separado do <see cref="CupomDto"/> porque o painel precisa do cadastro inteiro,
/// com validade e chave ativa, e o checkout só precisa do desconto. O que se
/// calcula no caminho é o desconto; o que se cadastra é a regra.
/// </summary>
public sealed record AplicacaoDeCupom(
    long CupomId,
    string Codigo,
    decimal Percentual,
    decimal Desconto);

public sealed record ItemDePedidoRequisicao(long ProdutoId, int Quantidade);

public sealed record EnderecoDeEntregaRequisicao(
    string Cep,
    string Logradouro,
    string Numero,
    string? Complemento,
    string Bairro,
    string Cidade,
    string Estado);

/// <summary>
/// Pedido a ser criado, comum ao checkout e à venda de balcão. O que muda entre
/// os dois é a <c>Origem</c> e o que vem preenchido: venda de balcão não traz
/// endereço e o frete é zero.
///
/// <paramref name="CustoFrete"/> entra como valor pronto, e não como cálculo.
/// Calcular a distância e a tarifa é do checkout, e essa regra ainda não existe
/// (ver <c>ConfiguracaoFrete</c> no roadmap). Trazer a fórmula para dentro do
/// pedido deixaria meio-cálculo em dois lugares.
/// </summary>
public sealed record RequisicaoDePedido(
    long UsuarioId,
    IReadOnlyList<ItemDePedidoRequisicao> Itens,
    EnderecoDeEntregaRequisicao? Endereco,
    string? CupomCodigo,
    string? Observacoes,
    decimal CustoFrete,
    decimal DistanciaKm,
    DateTime? CriadoEm);

public sealed record PedidoItemDto(
    long ProdutoId,
    string Nome,
    int Quantidade,
    decimal PrecoVendaUnitario,
    decimal? PrecoPromocionalUnitario,
    decimal Subtotal);

public sealed record PedidoDto(
    long Id,
    long UsuarioId,
    string NomeCliente,
    string? DocumentoCliente,
    string? TelefoneContato,
    string? EmailContato,
    OrigemPedido Origem,
    StatusPedido Status,
    MetodoPagamento? MetodoPagamento,
    string? CupomCodigo,
    decimal Subtotal,
    decimal Desconto,
    decimal CustoFrete,
    decimal Total,
    string? Observacoes,
    DateTime CriadoEm,
    DateTime? PagoEm,
    IReadOnlyList<PedidoItemDto> Itens);

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
