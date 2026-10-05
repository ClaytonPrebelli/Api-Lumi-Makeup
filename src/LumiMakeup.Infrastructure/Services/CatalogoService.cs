using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace LumiMakeup.Infrastructure.Services;

public sealed class CatalogoService : ICatalogoService
{
    private static readonly Expression<Func<Produto, ProdutoDto>> ProjetarProduto = p =>
        new ProdutoDto(
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
                .ToList(),
            p.Variantes
                .Where(v => v.Ativo)
                .OrderBy(v => v.Ordem)
                .ThenBy(v => v.Id)
                .Select(v => new VarianteProdutoLojaDto(
                    v.Id,
                    v.Nome,
                    v.CorHex,
                    v.QuantidadeEstoque,
                    v.PrecoAdicional,
                    v.Ativo,
                    v.Ordem))
                .ToList());

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

    public async Task<IReadOnlyList<BannerDto>> ObterBannersAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Banners
            .AsNoTracking()
            .Where(b => b.Ativo)
            .OrderBy(b => b.Ordem)
            .ThenBy(b => b.Id)
            .Select(b => new BannerDto(
                b.Id,
                b.CaminhoRelativoDesktop,
                b.CaminhoRelativoMobile,
                b.TextoAlternativo,
                b.Ordem))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProdutoDto>> ObterProdutosAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await ProjetarAsync(
            _contexto.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo)
                .OrderBy(p => p.Nome)
                .ThenBy(p => p.Id),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProdutoDto>> ObterProdutosDestaqueAsync(CancellationToken cancellationToken = default)
    {
        return await ProjetarAsync(
            _contexto.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.Destaque)
                .OrderBy(p => p.Nome)
                .ThenBy(p => p.Id),
            cancellationToken);
    }

    public async Task<ProdutosPaginadosDto> ObterProdutosPaginadosAsync(
        int pagina,
        int tamanhoPagina,
        string? categoriaSlug,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1 || tamanhoPagina is < 1 or > 100 ||
            pagina > int.MaxValue / tamanhoPagina)
        {
            throw new ArgumentOutOfRangeException(nameof(pagina), "Página ou tamanho de página inválido.");
        }

        var consulta = _contexto.Produtos
            .AsNoTracking()
            .Where(p => p.Ativo);

        if (!string.IsNullOrWhiteSpace(categoriaSlug))
        {
            var slugNormalizado = categoriaSlug.Trim();
            consulta = consulta.Where(p => p.Categoria.Slug == slugNormalizado && p.Categoria.Ativo);
        }

        var totalItens = await consulta.CountAsync(cancellationToken);
        var totalPaginas = (int)Math.Ceiling(totalItens / (double)tamanhoPagina);
        var itens = await consulta
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Select(ProjetarProduto)
            .ToListAsync(cancellationToken);

        return new ProdutosPaginadosDto(itens, pagina, tamanhoPagina, totalItens, totalPaginas);
    }

    private static async Task<IReadOnlyList<ProdutoDto>> ProjetarAsync(
        IQueryable<Produto> consulta,
        CancellationToken cancellationToken)
    {
        return await consulta
            .Select(ProjetarProduto)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProdutoDto?> ObterProdutoPorSlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _contexto.Produtos
            .AsNoTracking()
            .Where(p => p.Slug == slug && p.Ativo)
            .Select(ProjetarProduto)
            .SingleOrDefaultAsync(cancellationToken);
    }
}