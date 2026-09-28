using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class CatalogoService : ICatalogoService
{
    private readonly LumiDbContext _contexto;

    public CatalogoService(LumiDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<CategoriaDto>> ObterCategoriasAtivasAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Categorias
            .AsNoTracking()
            .Where(c => c.Ativo)
            .OrderBy(c => c.Nome)
            .Select(c => new CategoriaDto(c.Id, c.Nome, c.Slug, c.Descricao, c.Ativo))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await ProjetarAsync(
            _contexto.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProdutoDto>> ObterProdutosDestaqueAsync(CancellationToken cancellationToken = default)
    {
        return await ProjetarAsync(
            _contexto.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.Destaque)
                .OrderBy(p => p.Nome),
            cancellationToken);
    }

    private static async Task<IReadOnlyList<ProdutoDto>> ProjetarAsync(
        IQueryable<Produto> consulta,
        CancellationToken cancellationToken)
    {
        return await consulta
            .Select(p => new ProdutoDto(
                p.Id,
                p.Nome,
                p.Slug,
                p.Descricao,
                p.PrecoVenda,
                p.PrecoPromocional,
                p.QuantidadeEstoque,
                p.Ativo,
                p.Destaque,
                p.CategoriaId,
                p.Categoria.Nome,
                p.Imagens
                    .OrderBy(i => i.Ordem)
                    .Select(i => new ImagemProdutoDto(i.Id, i.CaminhoRelativo, i.NomeOriginal, i.Ordem))
                    .ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProdutoDto?> ObterProdutoPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _contexto.Produtos
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.Ativo)
            .Select(p => new ProdutoDto(
                p.Id,
                p.Nome,
                p.Slug,
                p.Descricao,
                p.PrecoVenda,
                p.PrecoPromocional,
                p.QuantidadeEstoque,
                p.Ativo,
                p.Destaque,
                p.CategoriaId,
                p.Categoria.Nome,
                p.Imagens
                    .OrderBy(i => i.Ordem)
                    .Select(i => new ImagemProdutoDto(i.Id, i.CaminhoRelativo, i.NomeOriginal, i.Ordem))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}