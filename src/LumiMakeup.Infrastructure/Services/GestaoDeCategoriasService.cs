using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeCategoriasService : IGestaoDeCategoriasService
{
    private readonly LumiDbContext _contexto;

    public GestaoDeCategoriasService(LumiDbContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<IReadOnlyList<CategoriaDto>> ObterTodasAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .Select(c => new CategoriaDto(c.Id, c.Nome, c.Slug, c.Descricao, c.Ativo))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoriaDto> CriarAsync(
        RequisicaoDeCategoria requisicao,
        CancellationToken cancellationToken = default)
    {
        Validar(requisicao);

        var categoria = new Categoria
        {
            Nome = requisicao.Nome.Trim(),
            Slug = await GerarSlugUnicoAsync(requisicao.Slug ?? requisicao.Nome, null, cancellationToken),
            Descricao = string.IsNullOrWhiteSpace(requisicao.Descricao) ? null : requisicao.Descricao.Trim(),
            Ativo = requisicao.Ativo
        };

        _contexto.Categorias.Add(categoria);
        await _contexto.SaveChangesAsync(cancellationToken);

        return new CategoriaDto(categoria.Id, categoria.Nome, categoria.Slug, categoria.Descricao, categoria.Ativo);
    }

    public async Task<CategoriaDto> AtualizarAsync(
        long id,
        RequisicaoDeCategoria requisicao,
        CancellationToken cancellationToken = default)
    {
        Validar(requisicao);

        var categoria = await _contexto.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");

        categoria.Nome = requisicao.Nome.Trim();
        categoria.Slug = await GerarSlugUnicoAsync(requisicao.Slug ?? requisicao.Nome, categoria.Id, cancellationToken);
        categoria.Descricao = string.IsNullOrWhiteSpace(requisicao.Descricao) ? null : requisicao.Descricao.Trim();
        categoria.Ativo = requisicao.Ativo;

        await _contexto.SaveChangesAsync(cancellationToken);

        return new CategoriaDto(categoria.Id, categoria.Nome, categoria.Slug, categoria.Descricao, categoria.Ativo);
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        var categoria = await _contexto.Categorias.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");

        var possuiProdutos = await _contexto.Produtos.AnyAsync(p => p.CategoriaId == id, cancellationToken);

        if (possuiProdutos)
        {
            throw new InvalidOperationException(
                "Esta categoria possui produtos vinculados e não pode ser excluída. Desative-a para ocultá-la na vitrine.");
        }

        _contexto.Categorias.Remove(categoria);
        await _contexto.SaveChangesAsync(cancellationToken);
    }

    private static void Validar(RequisicaoDeCategoria requisicao)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Nome))
        {
            throw new InvalidOperationException("O nome da categoria é obrigatório.");
        }
    }

    private async Task<string> GerarSlugUnicoAsync(
        string? slugInformado,
        long? categoriaId,
        CancellationToken cancellationToken)
    {
        var baseSlug = GestaoDeProdutosService.GerarSlug(slugInformado);

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "categoria";
        }

        var candidato = baseSlug;
        var sufixo = 2;

        while (await _contexto.Categorias
                   .AnyAsync(c => c.Slug == candidato && (categoriaId == null || c.Id != categoriaId), cancellationToken))
        {
            candidato = $"{baseSlug}-{sufixo}";
            sufixo++;
        }

        return candidato;
    }
}
