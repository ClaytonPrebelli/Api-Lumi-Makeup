using LumiMakeup.Api.Controllers;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LumiMakeup.Tests.Api;

public class AdminProdutosControllerTests
{
    private static readonly byte[] CabecalhoPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private static AdminProdutosController CriarController(
        Mock<IGestaoDeProdutosService> gestaoDeProdutos,
        long tamanhoMaximo = 5_242_880,
        Mock<IMelhoradorDeTextoService>? melhorador = null)
    {
        var opcoes = Options.Create(new ArmazenamentoDeImagensOptions
        {
            CaminhoBase = AppContext.BaseDirectory,
            TamanhoMaximoEmBytes = tamanhoMaximo
        });

        return new AdminProdutosController(
            gestaoDeProdutos.Object,
            (melhorador ?? new Mock<IMelhoradorDeTextoService>()).Object,
            opcoes,
            NullLogger<AdminProdutosController>.Instance);
    }

    private static ProdutoAdministracaoDto CriarProduto() => new(
        10, "Batom Matte", "batom-matte", "Batom de alta duração", 20m, 39.90m, null, 5, true, false,
        DateTime.UtcNow, 2, "Batom",
        new ImagemProdutoDto[] { new(1, "produtos/batom.png", "batom.png", 0) });

    private static RequisicaoDeProduto Requisicao() => new(2, "Batom Matte", null, "Batom", 20m, 39.90m, null, 5, true, false);

    private const int BytesDeCabecalhoLidos = 8;

    private static IFormFile CriarArquivo(string nome = "batom.png", int tamanho = 64)
    {
        var conteudo = new byte[BytesDeCabecalhoLidos + tamanho];
        CabecalhoPng.CopyTo(conteudo, 0);
        return new FormFile(new MemoryStream(conteudo), 0, conteudo.Length, "arquivo", nome);
    }

    private static string MensagemDe(object? resultado) =>
        (string)resultado!.GetType().GetProperty("message")!.GetValue(resultado)!;

    [Fact]
    public async Task ObterTodos_retorna_ok_com_a_lista()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var produtos = new[] { CriarProduto() };
        gestao.Setup(g => g.ObterTodosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(produtos);
        var controller = CriarController(gestao);

        var resultado = await controller.ObterTodos(CancellationToken.None);

        Assert.Equal(produtos, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task ObterPorId_retorna_ok_com_o_produto()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var produto = CriarProduto();
        gestao.Setup(g => g.ObterPorIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(produto);
        var controller = CriarController(gestao);

        var resultado = await controller.ObterPorId(10, CancellationToken.None);

        Assert.Equal(produto, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task ObterPorId_retorna_not_found_quando_inexistente()    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ObterPorIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProdutoAdministracaoDto?)null);
        var controller = CriarController(gestao);

        Assert.IsType<NotFoundResult>(await controller.ObterPorId(10, CancellationToken.None));
    }

    [Fact]
    public async Task Criar_retorna_created_com_o_produto()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var produto = CriarProduto();
        gestao.Setup(g => g.CriarAsync(It.IsAny<RequisicaoDeProduto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(produto);
        var controller = CriarController(gestao);

        var resultado = await controller.Criar(Requisicao(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.Equal(produto, created.Value);
        Assert.Equal(10L, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task Criar_retorna_bad_request_quando_a_regra_de_negocio_falha()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.CriarAsync(It.IsAny<RequisicaoDeProduto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Categoria inválida ou inativa."));
        var controller = CriarController(gestao);

        var resultado = await controller.Criar(Requisicao(), CancellationToken.None);

        Assert.Equal("Categoria inválida ou inativa.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task Atualizar_retorna_ok_com_o_produto()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var produto = CriarProduto();
        gestao.Setup(g => g.AtualizarAsync(10, It.IsAny<RequisicaoDeProduto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(produto);
        var controller = CriarController(gestao);

        var resultado = await controller.Atualizar(10, Requisicao(), CancellationToken.None);

        Assert.Equal(produto, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Atualizar_retorna_not_found_quando_produto_inexistente()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AtualizarAsync(10, It.IsAny<RequisicaoDeProduto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Produto não encontrado."));
        var controller = CriarController(gestao);

        var resultado = await controller.Atualizar(10, Requisicao(), CancellationToken.None);

        Assert.Equal("Produto não encontrado.", MensagemDe(Assert.IsType<NotFoundObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task Atualizar_retorna_bad_request_quando_a_regra_de_negocio_falha()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AtualizarAsync(10, It.IsAny<RequisicaoDeProduto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Categoria inválida ou inativa."));
        var controller = CriarController(gestao);

        var resultado = await controller.Atualizar(10, Requisicao(), CancellationToken.None);

        Assert.Equal("Categoria inválida ou inativa.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task Excluir_retorna_no_content()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var controller = CriarController(gestao);

        Assert.IsType<NoContentResult>(await controller.Excluir(10, CancellationToken.None));
        gestao.Verify(g => g.ExcluirAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excluir_retorna_not_found_quando_produto_inexistente()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ExcluirAsync(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Produto não encontrado."));
        var controller = CriarController(gestao);

        var resultado = await controller.Excluir(10, CancellationToken.None);

        Assert.Equal("Produto não encontrado.", MensagemDe(Assert.IsType<NotFoundObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task Excluir_retorna_bad_request_quando_o_produto_tem_pedidos()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ExcluirAsync(10, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Este produto está vinculado a pedidos e não pode ser excluído."));
        var controller = CriarController(gestao);

        var resultado = await controller.Excluir(10, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task AdicionarImagem_retorna_created_com_a_imagem()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var imagem = new ImagemProdutoDto(1, "produtos/abc.png", "batom.png", 0);
        gestao.Setup(g => g.AdicionarImagemAsync(10, It.IsAny<Stream>(), "batom.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(imagem);
        var controller = CriarController(gestao);

        var resultado = await controller.AdicionarImagem(10, CriarArquivo(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.Equal(imagem, created.Value);
        Assert.Equal(10L, created.RouteValues!["id"]);
    }

    [Fact]
    public async Task AdicionarImagem_retorna_bad_request_quando_nenhum_arquivo_e_enviado()
    {
        var controller = CriarController(new Mock<IGestaoDeProdutosService>());

        var resultado = await controller.AdicionarImagem(10, null!, CancellationToken.None);

        Assert.Equal("Selecione um arquivo de imagem.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AdicionarImagem_retorna_bad_request_quando_o_arquivo_e_vazio()
    {
        var controller = CriarController(new Mock<IGestaoDeProdutosService>());

        var vazio = new FormFile(new MemoryStream([]), 0, 0, "arquivo", "vazio.png");
        var resultado = await controller.AdicionarImagem(10, vazio, CancellationToken.None);

        Assert.Equal("Selecione um arquivo de imagem.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AdicionarImagem_retorna_bad_request_quando_excede_o_limite()
    {
        var controller = CriarController(new Mock<IGestaoDeProdutosService>(), tamanhoMaximo: 16);

        var resultado = await controller.AdicionarImagem(10, CriarArquivo(tamanho: 512), CancellationToken.None);

        Assert.Contains("excede o limite", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AdicionarImagem_retorna_not_found_quando_produto_inexistente()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AdicionarImagemAsync(10, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Produto não encontrado."));
        var controller = CriarController(gestao);

        var resultado = await controller.AdicionarImagem(10, CriarArquivo(), CancellationToken.None);

        Assert.Equal("Produto não encontrado.", MensagemDe(Assert.IsType<NotFoundObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AdicionarImagem_retorna_bad_request_quando_o_formato_e_recusado()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AdicionarImagemAsync(10, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Formato não permitido. Envie apenas: jpg, jpeg, png."));
        var controller = CriarController(gestao);

        var resultado = await controller.AdicionarImagem(10, CriarArquivo(), CancellationToken.None);

        Assert.Contains("Formato não permitido", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public void AdicionarImagem_devolve_500_com_mensagem_que_diz_a_causa(Type excecao)
    {
        /*
         * O bug que fechou o upload: estas excecoes escapavam do controller, e o
         * 500 saia sem Access-Control-Allow-Origin. O CorsMiddleware so aplica o
         * header quando a requisicao termina normalmente, entao o navegador
         * reportava "bloqueado pela politica de CORS" - que e uma configuracao
         * perfeita. O operador acabava mexendo no CORS enquanto o defeito era
         * disco cheio ou permissao de pasta.
         *
         * O teste exige as duas metades: status 500 E mensagem que nomeia a
         * excecao. So o status nao resolve - o 500 sem texto continua sendo
         * invisivel para quem opera.
         */
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AdicionarImagemAsync(10, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(excecao, "disco cheio"));
        var controller = CriarController(gestao);

        var resultado = controller.AdicionarImagem(10, CriarArquivo(), CancellationToken.None).GetAwaiter().GetResult();

        var erro = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status500InternalServerError, erro.StatusCode);

        var mensagem = MensagemDe(erro.Value);
        Assert.Contains(excecao.Name, mensagem);
        Assert.Contains("disco cheio", mensagem);
    }

    [Fact]
    public void AdicionarImagem_nao_transforma_falha_de_disco_em_erro_de_validacao()
    {
        /*
         * InvalidOperationException significa "recusei este arquivo" e vira 400.
         * Se IOException acabasse na mesma cesta, um disco cheio apareceria como
         * erro de validacao e o navegador diria que a imagem esta errada, quando
         * a imagem esta perfeita e o servidor e que esta sem espaco.
         */
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.AdicionarImagemAsync(10, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("sem espaco"));
        var controller = CriarController(gestao);

        var resultado = controller.AdicionarImagem(10, CriarArquivo(), CancellationToken.None).GetAwaiter().GetResult();

        Assert.IsNotType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task ExcluirImagem_retorna_no_content()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var controller = CriarController(gestao);

        Assert.IsType<NoContentResult>(await controller.ExcluirImagem(10, 1, CancellationToken.None));
        gestao.Verify(g => g.ExcluirImagemAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExcluirImagem_retorna_not_found_quando_a_imagem_nao_existe()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ExcluirImagemAsync(10, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Imagem não encontrada para este produto."));
        var controller = CriarController(gestao);

        var resultado = await controller.ExcluirImagem(10, 1, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task ReordenarImagens_retorna_ok_com_a_nova_ordem()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        var imagens = new[] { new ImagemProdutoDto(2, "produtos/b.png", "b.png", 0), new ImagemProdutoDto(1, "produtos/a.png", "a.png", 1) };
        gestao.Setup(g => g.ReordenarImagensAsync(10, It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(imagens);
        var controller = CriarController(gestao);

        var resultado = await controller.ReordenarImagens(10, new RequisicaoDeOrdenacaoDeImagens([2, 1]), CancellationToken.None);

        Assert.Equal(imagens, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task ReordenarImagens_retorna_not_found_quando_o_produto_nao_tem_imagens()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ReordenarImagensAsync(10, It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Produto não possui imagens."));
        var controller = CriarController(gestao);

        var resultado = await controller.ReordenarImagens(10, new RequisicaoDeOrdenacaoDeImagens([1]), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task ReordenarImagens_retorna_bad_request_quando_a_ordem_divergir()
    {
        var gestao = new Mock<IGestaoDeProdutosService>();
        gestao.Setup(g => g.ReordenarImagensAsync(10, It.IsAny<IReadOnlyList<long>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A ordem informada não corresponde às imagens do produto."));
        var controller = CriarController(gestao);

        var resultado = await controller.ReordenarImagens(10, new RequisicaoDeOrdenacaoDeImagens([1]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }
}

public class AdminCategoriasControllerTests
{
    private static RequisicaoDeCategoria Requisicao() => new("Base Líquida", null, "Maquiagem", true);

    private static string MensagemDe(object? resultado) =>
        (string)resultado!.GetType().GetProperty("message")!.GetValue(resultado)!;

    [Fact]
    public async Task ObterTodas_retorna_ok_com_a_lista()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        var categorias = new[] { new CategoriaDto(1, "Bases", "bases", "Maquiagem", true) };
        gestao.Setup(g => g.ObterTodasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categorias);
        var controller = new AdminCategoriasController(gestao.Object);

        var resultado = await controller.ObterTodas(CancellationToken.None);

        Assert.Equal(categorias, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Criar_retorna_created_com_a_categoria()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        var categoria = new CategoriaDto(1, "Bases", "bases", "Maquiagem", true);
        gestao.Setup(g => g.CriarAsync(It.IsAny<RequisicaoDeCategoria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoria);
        var controller = new AdminCategoriasController(gestao.Object);

        var resultado = await controller.Criar(Requisicao(), CancellationToken.None);

        Assert.Equal(categoria, Assert.IsType<CreatedAtActionResult>(resultado).Value);
    }

    [Fact]
    public async Task Criar_retorna_bad_request_quando_o_nome_e_vazio()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        gestao.Setup(g => g.CriarAsync(It.IsAny<RequisicaoDeCategoria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("O nome da categoria é obrigatório."));
        var controller = new AdminCategoriasController(gestao.Object);

        var resultado = await controller.Criar(Requisicao(), CancellationToken.None);

        Assert.Equal("O nome da categoria é obrigatório.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task Atualizar_retorna_ok_com_a_categoria()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        var categoria = new CategoriaDto(1, "Bases", "bases", "Maquiagem", false);
        gestao.Setup(g => g.AtualizarAsync(1, It.IsAny<RequisicaoDeCategoria>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(categoria);
        var controller = new AdminCategoriasController(gestao.Object);

        var resultado = await controller.Atualizar(1, Requisicao(), CancellationToken.None);

        Assert.Equal(categoria, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Atualizar_retorna_not_found_quando_inexistente()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        gestao.Setup(g => g.AtualizarAsync(1, It.IsAny<RequisicaoDeCategoria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Categoria não encontrada."));
        var controller = new AdminCategoriasController(gestao.Object);

        Assert.IsType<NotFoundObjectResult>(await controller.Atualizar(1, Requisicao(), CancellationToken.None));
    }

    [Fact]
    public async Task Atualizar_retorna_bad_request_quando_a_regra_de_negocio_falha()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        gestao.Setup(g => g.AtualizarAsync(1, It.IsAny<RequisicaoDeCategoria>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("O nome da categoria é obrigatório."));
        var controller = new AdminCategoriasController(gestao.Object);

        Assert.IsType<BadRequestObjectResult>(await controller.Atualizar(1, Requisicao(), CancellationToken.None));
    }

    [Fact]
    public async Task Excluir_retorna_no_content()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        var controller = new AdminCategoriasController(gestao.Object);

        Assert.IsType<NoContentResult>(await controller.Excluir(1, CancellationToken.None));
        gestao.Verify(g => g.ExcluirAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Excluir_retorna_not_found_quando_inexistente()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        gestao.Setup(g => g.ExcluirAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Categoria não encontrada."));
        var controller = new AdminCategoriasController(gestao.Object);

        Assert.IsType<NotFoundObjectResult>(await controller.Excluir(1, CancellationToken.None));
    }

    [Fact]
    public async Task Excluir_retorna_bad_request_quando_ha_produtos_vinculados()
    {
        var gestao = new Mock<IGestaoDeCategoriasService>();
        gestao.Setup(g => g.ExcluirAsync(1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Esta categoria possui produtos vinculados e não pode ser excluída."));
        var controller = new AdminCategoriasController(gestao.Object);

        Assert.IsType<BadRequestObjectResult>(await controller.Excluir(1, CancellationToken.None));
    }
}
