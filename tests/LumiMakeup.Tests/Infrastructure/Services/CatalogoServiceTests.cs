using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class CatalogoServiceTests
{
    [Fact]
    public async Task ObterCategoriasAtivasAsync_retorna_apenas_categorias_ativas_ordenadas_por_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Categorias.AddRange(
            new Categoria { Nome = "Bases", Slug = "bases", Descricao = "Bases", Ativo = true },
            new Categoria { Nome = "Batons", Slug = "batons", Descricao = "Batons", Ativo = false },
            new Categoria { Nome = "Brumas", Slug = "brumas", Descricao = null, Ativo = true });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var categorias = await servico.ObterCategoriasAtivasAsync(CancellationToken.None);

        Assert.Equal(2, categorias.Count);
        Assert.Equal("Bases", categorias[0].Nome);
        Assert.Equal("Brumas", categorias[1].Nome);
        Assert.True(categorias[0].Ativo);
        Assert.Null(categorias[1].Descricao);
    }

    [Fact]
    public async Task ObterCategoriasAtivasAsync_retorna_lista_vazia_quando_nao_existem_categorias()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var servico = new CatalogoService(contexto);
        var categorias = await servico.ObterCategoriasAtivasAsync(CancellationToken.None);

        Assert.Empty(categorias);
    }

    [Fact]
    public async Task ObterProdutosAtivosAsync_traz_o_preco_promocional_e_a_flag_de_destaque()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = new Categoria { Nome = "Bases", Slug = "bases", Ativo = true };
        contexto.Categorias.Add(categoria);
        contexto.Produtos.Add(new Produto
        {
            Nome = "Base em Promoção", Slug = "base-promocao", Descricao = "",
            PrecoVenda = 90m, PrecoPromocional = 59.90m,
            CategoriaId = categoria.Id, Categoria = categoria,
            Ativo = true, Destaque = true
        });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var produtos = await servico.ObterProdutosAtivosAsync(CancellationToken.None);

        var produto = Assert.Single(produtos);
        Assert.Equal(59.90m, produto.PrecoPromocional);
        Assert.True(produto.Destaque);
    }

    [Fact]
    public async Task ObterProdutosDestaqueAsync_traz_so_os_ativos_com_destaque()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = new Categoria { Nome = "Bases", Slug = "bases", Ativo = true };
        contexto.Categorias.Add(categoria);
        contexto.Produtos.AddRange(
            new Produto
            {
                Nome = "Vitrine", Slug = "vitrine", Descricao = "",
                PrecoVenda = 50m, CategoriaId = categoria.Id, Categoria = categoria,
                Ativo = true, Destaque = true
            },
            new Produto
            {
                Nome = "Comum", Slug = "comum", Descricao = "",
                PrecoVenda = 50m, CategoriaId = categoria.Id, Categoria = categoria,
                Ativo = true, Destaque = false
            },
            new Produto
            {
                // Destaque mas inativo: desativar tem de tirar da vitrine, senão
                // a home mostraria produto que o cliente não consegue comprar.
                Nome = "Desativado", Slug = "desativado", Descricao = "",
                PrecoVenda = 50m, CategoriaId = categoria.Id, Categoria = categoria,
                Ativo = false, Destaque = true
            });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var destaques = await servico.ObterProdutosDestaqueAsync(CancellationToken.None);

        var produto = Assert.Single(destaques);
        Assert.Equal("Vitrine", produto.Nome);
    }

    [Fact]
    public async Task ObterProdutosAtivosAsync_retorna_produtos_ativos_com_categoria_e_imagens_ordenadas()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = new Categoria { Nome = "Bases", Slug = "bases", Ativo = true };
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();

        var produto = new Produto
        {
            Nome = "Base Líquida",
            Slug = "base-liquida",
            Descricao = "Base",
            PrecoVenda = 59.9m,
            QuantidadeEstoque = 3,
            Ativo = true,
            CategoriaId = categoria.Id,
            Categoria = categoria
        };
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/1.jpg", NomeOriginal = "1.jpg", Ordem = 2 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/0.jpg", NomeOriginal = "0.jpg", Ordem = 1 });
        contexto.Produtos.Add(produto);
        contexto.Produtos.Add(new Produto { Nome = "Inativo", Slug = "inativo", Descricao = "", Ativo = false, Categoria = categoria, CategoriaId = categoria.Id });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var produtos = await servico.ObterProdutosAtivosAsync(CancellationToken.None);

        var produtoDto = Assert.Single(produtos);
        Assert.Equal("Base Líquida", produtoDto.Nome);
        Assert.Equal("base-liquida", produtoDto.Slug);
        Assert.Equal(59.9m, produtoDto.PrecoVenda);
        Assert.Equal(3, produtoDto.QuantidadeEstoque);
        Assert.True(produtoDto.Ativo);
        Assert.Equal(categoria.Id, produtoDto.CategoriaId);
        Assert.Equal("Bases", produtoDto.NomeCategoria);
        Assert.Equal(2, produtoDto.Imagens.Count);
        Assert.Equal("produtos/0.jpg", produtoDto.Imagens[0].CaminhoRelativo);
        Assert.Equal(1, produtoDto.Imagens[0].Ordem);
        Assert.Equal("produtos/1.jpg", produtoDto.Imagens[1].CaminhoRelativo);
    }

    [Fact]
    public async Task ObterProdutosAtivosAsync_retorna_lista_vazia_quando_sem_produtos()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var servico = new CatalogoService(contexto);
        var produtos = await servico.ObterProdutosAtivosAsync(CancellationToken.None);

        Assert.Empty(produtos);
    }

    [Fact]
    public async Task ObterProdutoPorSlugAsync_retorna_produto_ativo_pelo_slug()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = new Categoria { Nome = "Bases", Slug = "bases", Ativo = true };
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();

        var produto = new Produto
        {
            Nome = "Corretivo",
            Slug = "corretivo",
            Descricao = "Corretivo",
            PrecoVenda = 45m,
            Ativo = true,
            CategoriaId = categoria.Id,
            Categoria = categoria
        };
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/foto.jpg", NomeOriginal = "foto.jpg", Ordem = 0 });
        contexto.Produtos.Add(produto);
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var resultado = await servico.ObterProdutoPorSlugAsync("corretivo", CancellationToken.None);

        Assert.NotNull(resultado);
        Assert.Equal("Corretivo", resultado!.Nome);
        Assert.Equal("Bases", resultado.NomeCategoria);
        var imagem = Assert.Single(resultado.Imagens);
        Assert.Equal("produtos/foto.jpg", imagem.CaminhoRelativo);
    }

    [Fact]
    public async Task ObterProdutoPorSlugAsync_retorna_nulo_para_produto_inativo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Produtos.Add(new Produto { Nome = "Inativo", Slug = "inativo", Descricao = "", Ativo = false });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var resultado = await servico.ObterProdutoPorSlugAsync("inativo", CancellationToken.None);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ObterProdutoPorSlugAsync_retorna_nulo_para_slug_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var servico = new CatalogoService(contexto);
        var resultado = await servico.ObterProdutoPorSlugAsync("nao-existe", CancellationToken.None);

        Assert.Null(resultado);
    }
}