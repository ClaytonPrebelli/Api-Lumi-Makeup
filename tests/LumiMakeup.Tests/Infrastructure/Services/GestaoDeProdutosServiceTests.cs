using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeProdutosServiceTests
{
    private static RequisicaoDeProduto Requisicao(
        long categoriaId = 1,
        string nome = "Batom Matte",
        string? slug = null,
        decimal precoVenda = 39.90m,
        decimal precoCusto = 20m,
        int estoque = 5,
        decimal? precoPromocional = null,
        bool destaque = false) =>
        new(categoriaId, nome, slug, "Batom de alta duração", precoCusto, precoVenda, precoPromocional, estoque, true, destaque);

    private static GestaoDeProdutosService CriarServico(LumiDbContext contexto) =>
        new(contexto, Mock.Of<IArmazenamentoDeImagens>());

    private static async Task<Categoria> SemearCategoriaAsync(LumiDbContext contexto, bool ativa = true)
    {
        var categoria = new Categoria { Nome = "Batom", Slug = "batom", Ativo = ativa };
        contexto.Categorias.Add(categoria);
        await contexto.SaveChangesAsync();
        return categoria;
    }

    private static async Task<Produto> SemearProdutoAsync(LumiDbContext contexto, Categoria categoria)
    {
        var produto = new Produto
        {
            Nome = "Batom Matte",
            Slug = "batom-matte",
            Descricao = "Batom",
            PrecoVenda = 39.90m,
            PrecoCusto = 20m,
            CategoriaId = categoria.Id,
            Categoria = categoria,
            Ativo = true
        };
        contexto.Produtos.Add(produto);
        await contexto.SaveChangesAsync();
        return produto;
    }

    [Fact]
    public async Task ObterTodosAsync_traz_ativos_e_inativos_com_imagens_ordenadas()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 1 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/b.jpg", NomeOriginal = "b.jpg", Ordem = 0 });
        contexto.Produtos.Add(new Produto
        {
            Nome = "Inativo", Slug = "inativo", Descricao = "", CategoriaId = categoria.Id, Categoria = categoria, Ativo = false
        });
        await contexto.SaveChangesAsync();

        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());
        var produtos = await servico.ObterTodosAsync(CancellationToken.None);

        Assert.Equal(2, produtos.Count);
        var ativo = produtos.Single(p => p.Slug == "batom-matte");
        Assert.Equal(categoria.Nome, ativo.NomeCategoria);
        Assert.Equal(20m, ativo.PrecoCusto);
        Assert.Equal(new[] { "produtos/b.jpg", "produtos/a.jpg" }, ativo.Imagens.Select(i => i.CaminhoRelativo));
    }

    [Fact]
    public async Task ObterPorIdAsync_retorna_nulo_quando_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        Assert.Null(await servico.ObterPorIdAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_gera_slug_a_partir_do_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var produto = await servico.CriarAsync(Requisicao(categoria.Id, "Batom Matte Vermelho"), CancellationToken.None);

        Assert.Equal("batom-matte-vermelho", produto.Slug);
        Assert.Equal("Batom Matte Vermelho", produto.Nome);
        Assert.True(produto.Ativo);
        Assert.Empty(produto.Imagens);
    }

    [Fact]
    public async Task CriarAsync_aceita_slug_informado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var produto = await servico.CriarAsync(Requisicao(categoria.Id, slug: "batom-especial"), CancellationToken.None);

        Assert.Equal("batom-especial", produto.Slug);
    }

    [Fact]
    public async Task CriarAsync_desambigua_slug_duplicado_com_sufixo_numerico()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        await SemearProdutoAsync(contexto, categoria);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var produto = await servico.CriarAsync(Requisicao(categoria.Id), CancellationToken.None);

        Assert.Equal("batom-matte-2", produto.Slug);
    }

    [Fact]
    public async Task CriarAsync_exige_nome()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, nome: "   "), CancellationToken.None));

        Assert.Contains("nome", excecao.Message);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task CriarAsync_recusa_precos_negativos(decimal custo, decimal venda)
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, precoCusto: custo, precoVenda: venda), CancellationToken.None));

        Assert.Contains("negativos", excecao.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_estoque_negativo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, estoque: -1), CancellationToken.None));

        Assert.Contains("estoque", excecao.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_categoria_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoriaId: 999), CancellationToken.None));

        Assert.Contains("Categoria inválida", excecao.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_categoria_inativa()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto, ativa: false);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AtualizarAsync_altera_campos_do_produto()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var atualizado = await servico.AtualizarAsync(
            produto.Id,
            new RequisicaoDeProduto(categoria.Id, "Batom Novo", "batom-novo", "Nova descricao", 25m, 49.9m, null, 9, false, false),
            CancellationToken.None);

        Assert.Equal("Batom Novo", atualizado.Nome);
        Assert.Equal("batom-novo", atualizado.Slug);
        Assert.Equal("Nova descricao", atualizado.Descricao);
        Assert.Equal(25m, atualizado.PrecoCusto);
        Assert.Equal(49.9m, atualizado.PrecoVenda);
        Assert.Equal(9, atualizado.QuantidadeEstoque);
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task AtualizarAsync_lanca_para_produto_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.AtualizarAsync(999, Requisicao(categoria.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ExcluirAsync_remove_produto_e_apaga_arquivos_do_disco()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        await contexto.SaveChangesAsync();

        var armazenamento = new Mock<IArmazenamentoDeImagens>();
        var servico = new GestaoDeProdutosService(contexto, armazenamento.Object);

        await servico.ExcluirAsync(produto.Id, CancellationToken.None);

        Assert.Empty(contexto.Produtos.ToList());
        armazenamento.Verify(a => a.ExcluirAsync("produtos/a.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcluirAsync_lanca_para_produto_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => servico.ExcluirAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task AdicionarImagemAsync_grava_registro_com_ordem_seguinte()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/0.jpg", NomeOriginal = "0.jpg", Ordem = 0 });
        await contexto.SaveChangesAsync();

        var armazenamento = new Mock<IArmazenamentoDeImagens>();
        armazenamento.Setup(a => a.ArmazenarAsync(It.IsAny<Stream>(), "nova.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImagemArmazenada("produtos/abc.png", "nova.png", "image/png", 1024));

        var servico = new GestaoDeProdutosService(contexto, armazenamento.Object);
        using var conteudo = new MemoryStream([1, 2, 3]);

        var imagem = await servico.AdicionarImagemAsync(produto.Id, conteudo, "nova.png", CancellationToken.None);

        Assert.Equal(1, imagem.Ordem);
        Assert.Equal("produtos/abc.png", imagem.CaminhoRelativo);
        Assert.Equal("nova.png", imagem.NomeOriginal);
        Assert.Equal(2, contexto.ImagensProduto.Count());
    }

    [Fact]
    public async Task AdicionarImagemAsync_bloqueia_quarta_imagem()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        for (var i = 0; i < IGestaoDeProdutosService.MaximoDeImagensPorProduto; i++)
        {
            produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = $"produtos/{i}.jpg", NomeOriginal = $"{i}.jpg", Ordem = i });
        }
        await contexto.SaveChangesAsync();

        var armazenamento = new Mock<IArmazenamentoDeImagens>();
        var servico = new GestaoDeProdutosService(contexto, armazenamento.Object);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.AdicionarImagemAsync(produto.Id, new MemoryStream([1]), "extra.png", CancellationToken.None));

        Assert.Contains("limite de 3 imagens", excecao.Message);
        armazenamento.Verify(a => a.ArmazenarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdicionarImagemAsync_lanca_para_produto_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.AdicionarImagemAsync(999, new MemoryStream([1]), "a.png", CancellationToken.None));
    }

    [Fact]
    public async Task AdicionarImagemAsync_propaga_erro_de_armazenamento()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);

        var armazenamento = new Mock<IArmazenamentoDeImagens>();
        armazenamento.Setup(a => a.ArmazenarAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Formato não permitido."));

        var servico = new GestaoDeProdutosService(contexto, armazenamento.Object);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.AdicionarImagemAsync(produto.Id, new MemoryStream([1]), "a.gif", CancellationToken.None));

        Assert.Contains("Formato não permitido", excecao.Message);
        Assert.Empty(contexto.ImagensProduto.ToList());
    }

    [Fact]
    public async Task ExcluirImagemAsync_remove_registro_reindexa_e_apaga_arquivo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/b.jpg", NomeOriginal = "b.jpg", Ordem = 1 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/c.jpg", NomeOriginal = "c.jpg", Ordem = 2 });
        await contexto.SaveChangesAsync();

        var alvo = contexto.ImagensProduto.Single(i => i.CaminhoRelativo == "produtos/a.jpg");
        var armazenamento = new Mock<IArmazenamentoDeImagens>();
        var servico = new GestaoDeProdutosService(contexto, armazenamento.Object);

        await servico.ExcluirImagemAsync(produto.Id, alvo.Id, CancellationToken.None);

        var restantes = contexto.ImagensProduto.OrderBy(i => i.Ordem).ToList();
        Assert.Equal(2, restantes.Count);
        Assert.Equal(new[] { 0, 1 }, restantes.Select(i => i.Ordem));
        armazenamento.Verify(a => a.ExcluirAsync("produtos/a.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcluirImagemAsync_lanca_quando_a_imagem_nao_pertence_ao_produto()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        await contexto.SaveChangesAsync();
        var imagemId = contexto.ImagensProduto.Single().Id;

        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.ExcluirImagemAsync(produto.Id + 1, imagemId, CancellationToken.None));
    }

    [Fact]
    public async Task ReordenarImagensAsync_grava_a_nova_ordem()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/b.jpg", NomeOriginal = "b.jpg", Ordem = 1 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/c.jpg", NomeOriginal = "c.jpg", Ordem = 2 });
        await contexto.SaveChangesAsync();

        var ids = contexto.ImagensProduto.ToDictionary(i => i.CaminhoRelativo, i => i.Id);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var resultado = await servico.ReordenarImagensAsync(
            produto.Id,
            [ids["produtos/c.jpg"], ids["produtos/a.jpg"], ids["produtos/b.jpg"]],
            CancellationToken.None);

        Assert.Equal(
            new[] { "produtos/c.jpg", "produtos/a.jpg", "produtos/b.jpg" },
            resultado.Select(i => i.CaminhoRelativo));
        Assert.Equal(new[] { 0, 1, 2 }, resultado.Select(i => i.Ordem));
    }

    [Fact]
    public async Task ReordenarImagensAsync_recusa_conjunto_divergente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/b.jpg", NomeOriginal = "b.jpg", Ordem = 1 });
        await contexto.SaveChangesAsync();

        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReordenarImagensAsync(produto.Id, [1], CancellationToken.None));

        Assert.Contains("não corresponde", excecao.Message);
    }

    [Fact]
    public async Task ReordenarImagensAsync_recusa_ids_duplicados()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/a.jpg", NomeOriginal = "a.jpg", Ordem = 0 });
        produto.Imagens.Add(new ImagemProduto { CaminhoRelativo = "produtos/b.jpg", NomeOriginal = "b.jpg", Ordem = 1 });
        await contexto.SaveChangesAsync();

        var ids = contexto.ImagensProduto.Select(i => i.Id).ToList();
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ReordenarImagensAsync(produto.Id, [ids[0], ids[0]], CancellationToken.None));
    }

    [Fact]
    public async Task ReordenarImagensAsync_lanca_quando_o_produto_nao_tem_imagens()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var produto = await SemearProdutoAsync(contexto, categoria);
        var servico = new GestaoDeProdutosService(contexto, Mock.Of<IArmazenamentoDeImagens>());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.ReordenarImagensAsync(produto.Id, [], CancellationToken.None));
    }

    [Theory]
    [InlineData("Batom Matte", "batom-matte")]
    [InlineData("  Base Líquida HD  ", "base-liquida-hd")]
    [InlineData("Pincéu de Olhos", "pinceu-de-olhos")]
    [InlineData("Ácido Hialurônico", "acido-hialuronico")]
    [InlineData("Base 4x4", "base-4x4")]
    [InlineData("---", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void GerarSlug_normaliza_o_texto(string? entrada, string esperado)
    {
        Assert.Equal(esperado, GestaoDeProdutosService.GerarSlug(entrada));
    }

    [Fact]
    public async Task CriarAsync_guarda_o_preco_promocional()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        var produto = await servico.CriarAsync(
            Requisicao(categoria.Id, precoPromocional: 29.90m), CancellationToken.None);

        Assert.Equal(29.90m, produto.PrecoPromocional);
    }

    [Fact]
    public async Task CriarAsync_sem_promocao_deixa_o_preco_promocional_nulo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        var produto = await servico.CriarAsync(Requisicao(categoria.Id), CancellationToken.None);

        Assert.Null(produto.PrecoPromocional);
    }

    [Fact]
    public async Task CriarAsync_recusa_promocao_acima_do_preco_de_venda()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        // "Promover" para mais caro mostraria R$ 50 riscado com R$ 80 ao lado.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, precoPromocional: 50m), CancellationToken.None));

        Assert.Contains("menor que o preço de venda", erro.Message);
    }

    [Fact]
    public async Task CriarAsync_recusa_promocao_igual_ao_preco_de_venda()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        // Igual não é promoção, e deixaria a vitrine exibindo um selo sem ganho nenhum.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, precoVenda: 30m, precoPromocional: 30m), CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_recusa_promocao_negativa()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao(categoria.Id, precoPromocional: -1m), CancellationToken.None));
    }

    [Fact]
    public async Task AtualizarAsync_altera_o_preco_promocional_e_pode_limpar()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        var criado = await servico.CriarAsync(
            Requisicao(categoria.Id, precoPromocional: 25m), CancellationToken.None);

        var comPromocao = await servico.AtualizarAsync(
            criado.Id, Requisicao(categoria.Id, precoPromocional: 19.90m), CancellationToken.None);
        Assert.Equal(19.90m, comPromocao.PrecoPromocional);

        // Limpar a promoção é o caminho normal para encerrar a campanha.
        var semPromocao = await servico.AtualizarAsync(
            criado.Id, Requisicao(categoria.Id, precoPromocional: null), CancellationToken.None);
        Assert.Null(semPromocao.PrecoPromocional);
    }

    [Fact]
    public async Task AtualizarAsync_recusa_promocao_acima_do_preco_de_venda()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        var criado = await servico.CriarAsync(Requisicao(categoria.Id), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.AtualizarAsync(
                criado.Id, Requisicao(categoria.Id, precoVenda: 30m, precoPromocional: 31m), CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_guarda_a_flag_de_destaque()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var categoria = await SemearCategoriaAsync(contexto);
        var servico = CriarServico(contexto);

        var emDestaque = await servico.CriarAsync(
            Requisicao(categoria.Id, nome: "Destaque", destaque: true), CancellationToken.None);
        var comum = await servico.CriarAsync(
            Requisicao(categoria.Id, nome: "Comum"), CancellationToken.None);

        Assert.True(emDestaque.Destaque);
        Assert.False(comum.Destaque);
    }
}
