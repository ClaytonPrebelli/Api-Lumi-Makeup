using LumiMakeup.Aplicacao.DTOs;

namespace LumiMakeup.Aplicacao.Abstracoes;

public interface IServicoDeCatalogo
{
    Task<IReadOnlyList<CategoriaDto>> ObterCategoriasAtivasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default);
    Task<ProdutoDto?> ObterProdutoPorSlugAsync(string slug, CancellationToken cancellationToken = default);
}