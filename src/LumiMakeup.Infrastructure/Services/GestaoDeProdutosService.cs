using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Infrastructure.Services;

public sealed class GestaoDeProdutosService : IGestaoDeProdutosService
{
    private readonly LumiDbContext _contexto;
    private readonly IArmazenamentoDeImagens _armazenamento;

    public GestaoDeProdutosService(LumiDbContext contexto, IArmazenamentoDeImagens armazenamento)
    {
        _contexto = contexto;
        _armazenamento = armazenamento;
    }

    public async Task<IReadOnlyList<ProdutoAdministracaoDto>> ObterTodosAsync(CancellationToken cancellationToken = default)
    {
        return await _contexto.Produtos
            .AsNoTracking()
            .OrderBy(p => p.Nome)
            .Select(Projetar)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProdutoAdministracaoDto?> ObterPorIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _contexto.Produtos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(Projetar)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProdutoAdministracaoDto> CriarAsync(
        RequisicaoDeProduto requisicao,
        CancellationToken cancellationToken = default)
    {
        Validar(requisicao);
        await ValidarCategoriaAsync(requisicao.CategoriaId, cancellationToken);

        var produto = new Produto
        {
            CategoriaId = requisicao.CategoriaId,
            Nome = requisicao.Nome.Trim(),
            Slug = await GerarSlugUnicoAsync(requisicao.Slug ?? requisicao.Nome, null, cancellationToken),
            Descricao = requisicao.Descricao?.Trim() ?? string.Empty,
            PrecoCusto = requisicao.PrecoCusto,
            PrecoVenda = requisicao.PrecoVenda,
            QuantidadeEstoque = requisicao.QuantidadeEstoque,
            Ativo = requisicao.Ativo,
            CriadoEm = DateTime.UtcNow
        };

        _contexto.Produtos.Add(produto);
        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(produto.Id, cancellationToken)
            ?? throw new InvalidOperationException("Produto criado, mas não pôde ser relido.");
    }

    public async Task<ProdutoAdministracaoDto> AtualizarAsync(
        long id,
        RequisicaoDeProduto requisicao,
        CancellationToken cancellationToken = default)
    {
        Validar(requisicao);
        await ValidarCategoriaAsync(requisicao.CategoriaId, cancellationToken);

        var produto = await _contexto.Produtos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        produto.CategoriaId = requisicao.CategoriaId;
        produto.Nome = requisicao.Nome.Trim();
        produto.Slug = await GerarSlugUnicoAsync(requisicao.Slug ?? requisicao.Nome, produto.Id, cancellationToken);
        produto.Descricao = requisicao.Descricao?.Trim() ?? string.Empty;
        produto.PrecoCusto = requisicao.PrecoCusto;
        produto.PrecoVenda = requisicao.PrecoVenda;
        produto.QuantidadeEstoque = requisicao.QuantidadeEstoque;
        produto.Ativo = requisicao.Ativo;

        await _contexto.SaveChangesAsync(cancellationToken);

        return await ObterPorIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Produto atualizado, mas não pôde ser relido.");
    }

    public async Task ExcluirAsync(long id, CancellationToken cancellationToken = default)
    {
        var produto = await _contexto.Produtos
            .Include(p => p.Imagens)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        var caminhos = produto.Imagens.Select(i => i.CaminhoRelativo).ToList();

        _contexto.Produtos.Remove(produto);

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "Este produto está vinculado a pedidos e não pode ser excluído. Desative-o para ocultá-lo na vitrine.");
        }

        foreach (var caminho in caminhos)
        {
            await _armazenamento.ExcluirAsync(caminho, cancellationToken);
        }
    }

    public async Task<ImagemProdutoDto> AdicionarImagemAsync(
        long produtoId,
        Stream conteudo,
        string nomeOriginal,
        CancellationToken cancellationToken = default)
    {
        var produto = await _contexto.Produtos
            .Include(p => p.Imagens)
            .FirstOrDefaultAsync(p => p.Id == produtoId, cancellationToken)
            ?? throw new KeyNotFoundException("Produto não encontrado.");

        if (produto.Imagens.Count >= IGestaoDeProdutosService.MaximoDeImagensPorProduto)
        {
            throw new InvalidOperationException(
                $"O produto já possui o limite de {IGestaoDeProdutosService.MaximoDeImagensPorProduto} imagens.");
        }

        var armazenada = await _armazenamento.ArmazenarAsync(conteudo, nomeOriginal, cancellationToken);

        var imagem = new ImagemProduto
        {
            ProdutoId = produtoId,
            CaminhoRelativo = armazenada.CaminhoRelativo,
            NomeOriginal = armazenada.NomeOriginal,
            Ordem = produto.Imagens.Count
        };

        _contexto.ImagensProduto.Add(imagem);

        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _armazenamento.ExcluirAsync(armazenada.CaminhoRelativo, cancellationToken);
            throw;
        }

        return new ImagemProdutoDto(imagem.Id, imagem.CaminhoRelativo, imagem.NomeOriginal, imagem.Ordem);
    }

    public async Task ExcluirImagemAsync(
        long produtoId,
        long imagemId,
        CancellationToken cancellationToken = default)
    {
        var imagem = await _contexto.ImagensProduto
            .FirstOrDefaultAsync(i => i.Id == imagemId && i.ProdutoId == produtoId, cancellationToken)
            ?? throw new KeyNotFoundException("Imagem não encontrada para este produto.");

        var caminho = imagem.CaminhoRelativo;

        _contexto.ImagensProduto.Remove(imagem);
        await ReindexarOrdemAsync(produtoId, imagem.Id, cancellationToken);
        await _contexto.SaveChangesAsync(cancellationToken);

        await _armazenamento.ExcluirAsync(caminho, cancellationToken);
    }

    public async Task<IReadOnlyList<ImagemProdutoDto>> ReordenarImagensAsync(
        long produtoId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken = default)
    {
        var imagens = await _contexto.ImagensProduto
            .Where(i => i.ProdutoId == produtoId)
            .ToListAsync(cancellationToken);

        if (imagens.Count == 0)
        {
            throw new KeyNotFoundException("Produto não possui imagens.");
        }

        var existentes = imagens.Select(i => i.Id).ToHashSet();
        var recebidos = ids.Distinct().ToList();

        if (recebidos.Count != imagens.Count || !existentes.SetEquals(recebidos))
        {
            throw new InvalidOperationException("A ordem informada não corresponde às imagens do produto.");
        }

        var porId = imagens.ToDictionary(i => i.Id);

        for (var posicao = 0; posicao < recebidos.Count; posicao++)
        {
            porId[recebidos[posicao]].Ordem = posicao;
        }

        await _contexto.SaveChangesAsync(cancellationToken);

        return await _contexto.ImagensProduto
            .AsNoTracking()
            .Where(i => i.ProdutoId == produtoId)
            .OrderBy(i => i.Ordem)
            .Select(i => new ImagemProdutoDto(i.Id, i.CaminhoRelativo, i.NomeOriginal, i.Ordem))
            .ToListAsync(cancellationToken);
    }

    private async Task ReindexarOrdemAsync(long produtoId, long excluirId, CancellationToken cancellationToken)
    {
        var restantes = await _contexto.ImagensProduto
            .Where(i => i.ProdutoId == produtoId && i.Id != excluirId)
            .OrderBy(i => i.Ordem)
            .ToListAsync(cancellationToken);

        for (var posicao = 0; posicao < restantes.Count; posicao++)
        {
            restantes[posicao].Ordem = posicao;
        }
    }

    private async Task ValidarCategoriaAsync(long categoriaId, CancellationToken cancellationToken)
    {
        var existe = await _contexto.Categorias
            .AnyAsync(c => c.Id == categoriaId && c.Ativo, cancellationToken);

        if (!existe)
        {
            throw new InvalidOperationException("Categoria inválida ou inativa.");
        }
    }

    private static void Validar(RequisicaoDeProduto requisicao)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Nome))
        {
            throw new InvalidOperationException("O nome do produto é obrigatório.");
        }

        if (requisicao.PrecoVenda < 0 || requisicao.PrecoCusto < 0)
        {
            throw new InvalidOperationException("Os preços não podem ser negativos.");
        }

        if (requisicao.QuantidadeEstoque < 0)
        {
            throw new InvalidOperationException("A quantidade em estoque não pode ser negativa.");
        }
    }

    private async Task<string> GerarSlugUnicoAsync(
        string? slugInformado,
        long? produtoId,
        CancellationToken cancellationToken)
    {
        var baseSlug = GerarSlug(slugInformado);

        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "produto";
        }

        var candidato = baseSlug;
        var sufixo = 2;

        while (await _contexto.Produtos
                   .AnyAsync(p => p.Slug == candidato && (produtoId == null || p.Id != produtoId), cancellationToken))
        {
            candidato = $"{baseSlug}-{sufixo}";
            sufixo++;
        }

        return candidato;
    }

    internal static string GerarSlug(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var normalizado = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var construtor = new StringBuilder(normalizado.Length);
        var ultimoFoiSeparador = false;

        foreach (var caractere in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(caractere))
            {
                construtor.Append(caractere);
                ultimoFoiSeparador = false;
            }
            else if (!ultimoFoiSeparador)
            {
                construtor.Append('-');
                ultimoFoiSeparador = true;
            }
        }

        return construtor.ToString().Trim('-');
    }

    private static Expression<Func<Produto, ProdutoAdministracaoDto>> Projetar => p =>
        new ProdutoAdministracaoDto(
            p.Id,
            p.Nome,
            p.Slug,
            p.Descricao,
            p.PrecoCusto,
            p.PrecoVenda,
            p.QuantidadeEstoque,
            p.Ativo,
            p.CriadoEm,
            p.CategoriaId,
            p.Categoria.Nome,
            p.Imagens
                .OrderBy(i => i.Ordem)
                .Select(i => new ImagemProdutoDto(i.Id, i.CaminhoRelativo, i.NomeOriginal, i.Ordem))
                .ToList());
}
