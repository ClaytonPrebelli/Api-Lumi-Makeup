using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Application.Abstractions;

public interface ICatalogoService
{
    Task<IReadOnlyList<CategoriaDto>> ObterCategoriasAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default);
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
