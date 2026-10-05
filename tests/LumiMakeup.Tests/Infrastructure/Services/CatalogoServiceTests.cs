using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class CatalogoServiceTests
{
    [Fact]
    public async Task ObterBannersAtivosAsync_traz_so_os_ativos_na_ordem_do_carrossel()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Banners.AddRange(
            new Banner
            {
                CaminhoRelativoDesktop = "banners/d2.png",
                CaminhoRelativoMobile = "banners/m2.png",
                NomeOriginalDesktop = "d2.png",
                NomeOriginalMobile = "m2.png",
                Ordem = 2,
                Ativo = true
            },
            new Banner
            {
                CaminhoRelativoDesktop = "banners/d0.png",
                CaminhoRelativoMobile = "banners/m0.png",
                NomeOriginalDesktop = "d0.png",
                NomeOriginalMobile = "m0.png",
                Ordem = 0,
                Ativo = false
            },
            new Banner
            {
                CaminhoRelativoDesktop = "banners/d1.png",
                CaminhoRelativoMobile = "banners/m1.png",
                NomeOriginalDesktop = "d1.png",
                NomeOriginalMobile = "m1.png",
                Ordem = 1,
                Ativo = true
            });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var banners = await servico.ObterBannersAtivosAsync(CancellationToken.None);

        Assert.Equal(2, banners.Count);
        Assert.Equal(new[] { 1, 2 }, banners.Select(b => b.Ordem));
        Assert.All(banners, b => Assert.True(b.Ordem > 0));
    }

    [Fact]
    public async Task ObterBannersAtivosAsync_nao_expoe_dados_de_administracao()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Banners.Add(new Banner
        {
            CaminhoRelativoDesktop = "banners/d.png",
            CaminhoRelativoMobile = "banners/m.png",
            NomeOriginalDesktop = "confidencial.png",
            NomeOriginalMobile = "confidencial-celular.png",
            TextoAlternativo = "Banner",
            Ordem = 0,
            Ativo = true
        });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var banner = Assert.Single(await servico.ObterBannersAtivosAsync(CancellationToken.None));

        // A vitrine nao precisa do nome que o cliente tinha no disco, e exibi-lo
        // entregaria um detalhe de organizacao de arquivos sem utilidade.
        Assert.Null(banner.GetType().GetProperty("NomeOriginalDesktop"));
        Assert.Null(banner.GetType().GetProperty("CriadoEm"));
        Assert.Equal("banners/d.png", banner.CaminhoRelativoDesktop);
        Assert.Equal("banners/m.png", banner.CaminhoRelativoMobile);
    }

    [Fact]
    public async Task ObterBannersAtivosAsync_devolve_lista_vazia_sem_banner_ativo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        contexto.Banners.Add(new Banner
        {
            CaminhoRelativoDesktop = "banners/d.png",
            CaminhoRelativoMobile = "banners/m.png",
            NomeOriginalDesktop = "d.png",
            NomeOriginalMobile = "m.png",
            Ordem = 0,
            Ativo = false
        });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);

        // Lista vazia, e nao null: a home esconde o carrossel e cai no hero de
        // texto. Um null aqui quebraria o template.
        Assert.Empty(await servico.ObterBannersAtivosAsync(CancellationToken.None));
    }

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
    public async Task ObterProdutosPaginadosAsync_filtra_categoria_ordena_de_forma_estavel_e_expoe_opcoes_ativas()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = new Categoria { Nome = "Batons", Slug = "batons", Ativo = true };
        var outraCategoria = new Categoria { Nome = "Bases", Slug = "bases", Ativo = true };
        contexto.Categorias.AddRange(categoria, outraCategoria);
        await contexto.SaveChangesAsync();

        var mesmoNomePrimeiro = new Produto
        {
            Nome = "Batom", Slug = "batom-1", Descricao = "", Categoria = categoria, CategoriaId = categoria.Id,
            PrecoVenda = 40m, QuantidadeEstoque = 8, Ativo = true
        };
        mesmoNomePrimeiro.Variantes.Add(new VarianteProduto
        {
            Nome = "Vinho", CorHex = null, QuantidadeEstoque = 3, PrecoAdicional = 2m, Ativo = true, Ordem = 1
        });
        mesmoNomePrimeiro.Variantes.Add(new VarianteProduto
        {
            Nome = "Inativa", QuantidadeEstoque = 10, Ativo = false, Ordem = 0
        });
        var mesmoNomeSegundo = new Produto
        {
            Nome = "Batom", Slug = "batom-2", Descricao = "", Categoria = categoria, CategoriaId = categoria.Id,
            PrecoVenda = 45m, QuantidadeEstoque = 5, Ativo = true
        };
        contexto.Produtos.AddRange(
            mesmoNomePrimeiro,
            mesmoNomeSegundo,
            new Produto
            {
                Nome = "Base", Slug = "base", Descricao = "", Categoria = outraCategoria, CategoriaId = outraCategoria.Id,
                PrecoVenda = 20m, Ativo = true
            });
        await contexto.SaveChangesAsync();

        var servico = new CatalogoService(contexto);
        var resultado = await servico.ObterProdutosPaginadosAsync(1, 1, "batons", CancellationToken.None);

        Assert.Equal(2, resultado.TotalItens);
        Assert.Equal(2, resultado.TotalPaginas);
        var produto = Assert.Single(resultado.Itens);
        Assert.Equal("batom-1", produto.Slug);
        var variante = Assert.Single(produto.Variantes);
        Assert.Equal("Vinho", variante.Nome);
        Assert.Null(variante.CorHex);
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