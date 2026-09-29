using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface ICatalogoService
{
    Task<IReadOnlyList<CategoriaDto>> ObterCategoriasAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Produtos marcados para a vitrine da home, entre os ativos.</summary>
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosDestaqueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Banners ativos e ordenados que abrem a home. Sai daqui, e nao do
    /// <see cref="IGestaoDeBannersService"/>, porque a vitrine publica nunca deve
    /// enxergar banner inativo nem os dados de administracao.
    /// </summary>
    Task<IReadOnlyList<BannerDto>> ObterBannersAtivosAsync(CancellationToken cancellationToken = default);

    Task<ProdutoDto?> ObterProdutoPorSlugAsync(string slug, CancellationToken cancellationToken = default);
}

public interface IGestaoDeProdutosService
{
    const int MaximoDeImagensPorProduto = 3;

    Task<IReadOnlyList<ProdutoAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto> CriarAsync(RequisicaoDeProduto requisicao, CancellationToken cancellationToken = default);
    Task<ProdutoAdministracaoDto> AtualizarAsync(long id, RequisicaoDeProduto requisicao, CancellationToken cancellationToken = default);
    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);
    Task<ImagemProdutoDto> AdicionarImagemAsync(long produtoId, Stream conteudo, string nomeOriginal, CancellationToken cancellationToken = default);
    Task ExcluirImagemAsync(long produtoId, long imagemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImagemProdutoDto>> ReordenarImagensAsync(long produtoId, IReadOnlyList<long> ids, CancellationToken cancellationToken = default);
}

public interface IGestaoDeCategoriasService
{
    Task<IReadOnlyList<CategoriaDto>> ObterTodasAsync(CancellationToken cancellationToken = default);
    Task<CategoriaDto> CriarAsync(RequisicaoDeCategoria requisicao, CancellationToken cancellationToken = default);
    Task<CategoriaDto> AtualizarAsync(long id, RequisicaoDeCategoria requisicao, CancellationToken cancellationToken = default);
    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);
}

public interface IGestaoDeBannersService
{
    /// <summary>
    /// Quantidade de slides do carrossel. Limite rigido, e nao um aviso: o
    /// layout da home foi desenhado para tres, e um quarto slide empurra o
    /// restante para fora da dobra em telas de celular.
    /// </summary>
    const int MaximoDeBanners = 3;

    /// <summary>Todos os banners, ativos ou nao, ja ordenados. Uso do painel.</summary>
    Task<IReadOnlyList<BannerAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default);

    /// <summary>Banners ativos e ordenados, que sao os que aparecem na vitrine.</summary>
    Task<IReadOnlyList<BannerDto>> ObterAtivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria um slide com as duas imagens ja gravadas. Ordem e Ativo ficam de
    /// fora da requisicao de proposito: sao decididos pela reordenacao e pela
    /// ativacao, para nao misturar duas operacoes que mexem no mesmo par de
    /// campos.
    /// </summary>
    Task<BannerAdministracaoDto> CriarAsync(
        Stream imagemDesktop,
        string nomeOriginalDesktop,
        Stream imagemMobile,
        string nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default);

    Task<BannerAdministracaoDto> AtualizarAsync(
        long id,
        Stream? imagemDesktop,
        string? nomeOriginalDesktop,
        Stream? imagemMobile,
        string? nomeOriginalMobile,
        string? textoAlternativo,
        CancellationToken cancellationToken = default);

    Task<BannerAdministracaoDto> DefinirAtivoAsync(long id, bool ativo, CancellationToken cancellationToken = default);

    Task ExcluirAsync(long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BannerAdministracaoDto>> ReordenarAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken = default);
}
