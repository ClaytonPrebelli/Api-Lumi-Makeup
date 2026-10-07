using LumiMakeup.Api.Controllers;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Tests.Helpers;
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
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminProdutosController>());
    }

    private static ProdutoAdministracaoDto CriarProduto() => new(
        10, "Batom Matte", "batom-matte", "Batom de alta duração", 20m, 39.90m, null, 5, true, false,
        DateTime.UtcNow, 2, "Batom",
        new ImagemProdutoDto[] { new(1, "produtos/batom.png", "batom.png", 0) },
        Array.Empty<VarianteProdutoDto>());

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

public class AdminPedidosControllerTests
{
    private static AdminPedidosController CriarController(
        Mock<IGestaoDePedidosService> pedidos,
        long tamanhoMaximo = 5_242_880)
    {
        var opcoes = Options.Create(new ArmazenamentoDeImagensOptions
        {
            CaminhoBase = AppContext.BaseDirectory,
            TamanhoMaximoEmBytes = tamanhoMaximo
        });

        return new AdminPedidosController(pedidos.Object, opcoes);
    }

    private static PedidoDto PedidoComprovante(long id = 7) => new(
        id, 42, "Ana", null, "11999999999", "ana@exemplo.com",
        OrigemPedido.Online, StatusPedido.Pago, MetodoPagamento.Pix,
        null, 100m, 0m, 0m, 100m, null,
        DateTime.UtcNow, DateTime.UtcNow,
        null, null, null, null, null, null, null,
        Array.Empty<PedidoItemDto>(),
        "comprovantes/abc123.png", "comp.png");

    private static IFormFile CriarArquivo(string nome = "comp.png", int tamanho = 64)
    {
        var cabecalho = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var conteudo = new byte[8 + tamanho];
        cabecalho.CopyTo(conteudo, 0);
        return new FormFile(new MemoryStream(conteudo), 0, conteudo.Length, "comprovante", nome);
    }

    private static string MensagemDe(object? resultado) =>
        (string)resultado!.GetType().GetProperty("message")!.GetValue(resultado)!;

    [Fact]
    public async Task AnexarComprovante_retorna_ok_com_o_pedido()
    {
        var gestao = new Mock<IGestaoDePedidosService>();
        var pedido = PedidoComprovante();
        gestao.Setup(g => g.AnexarComprovanteAsync(7, It.IsAny<Stream>(), "comp.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pedido);
        var controller = CriarController(gestao);

        var resultado = await controller.AnexarComprovante(7, CriarArquivo(), CancellationToken.None);

        Assert.Equal(pedido, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task AnexarComprovante_retorna_bad_request_quando_nenhum_arquivo_e_enviado()
    {
        var controller = CriarController(new Mock<IGestaoDePedidosService>());

        var resultado = await controller.AnexarComprovante(7, null!, CancellationToken.None);

        Assert.Equal("Selecione um arquivo de imagem.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AnexarComprovante_retorna_bad_request_quando_o_arquivo_e_vazio()
    {
        var controller = CriarController(new Mock<IGestaoDePedidosService>());

        var vazio = new FormFile(new MemoryStream([]), 0, 0, "comprovante", "vazio.png");
        var resultado = await controller.AnexarComprovante(7, vazio, CancellationToken.None);

        Assert.Equal("Selecione um arquivo de imagem.", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AnexarComprovante_retorna_bad_request_quando_excede_o_limite()
    {
        var controller = CriarController(new Mock<IGestaoDePedidosService>(), tamanhoMaximo: 16);

        var resultado = await controller.AnexarComprovante(7, CriarArquivo(tamanho: 512), CancellationToken.None);

        Assert.Contains("excede o limite", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AnexarComprovante_retorna_not_found_quando_pedido_inexistente()
    {
        var gestao = new Mock<IGestaoDePedidosService>();
        gestao.Setup(g => g.AnexarComprovanteAsync(7, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Pedido não encontrado."));
        var controller = CriarController(gestao);

        var resultado = await controller.AnexarComprovante(7, CriarArquivo(), CancellationToken.None);

        Assert.Equal("Pedido não encontrado.", MensagemDe(Assert.IsType<NotFoundObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task AnexarComprovante_retorna_bad_request_quando_pedido_cancelado()
    {
        var gestao = new Mock<IGestaoDePedidosService>();
        gestao.Setup(g => g.AnexarComprovanteAsync(7, It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Este pedido está cancelado e não pode receber comprovante."));
        var controller = CriarController(gestao);

        var resultado = await controller.AnexarComprovante(7, CriarArquivo(), CancellationToken.None);

        Assert.Contains("cancelado", MensagemDe(Assert.IsType<BadRequestObjectResult>(resultado).Value));
    }
}

public class AdminWhatsAppControllerTests
{
    private static AdminWhatsAppController CriarController(
        Mock<IWhatsAppService> whatsApp,
        RepositorioDeSessaoWhatsApp sessao) =>
        new(
            whatsApp.Object,
            new Mock<IGestaoDeWhatsAppService>().Object,
            sessao,
            NullLogger<AdminWhatsAppController>.Instance);

    private static StatusDoWhatsApp Ligado() =>
        new(true, true, "5515999999999", "Lumi", null, null, null);

    [Fact]
    public async Task Status_diz_se_ha_sessao_guardada_no_banco()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var sessao = new RepositorioDeSessaoWhatsApp(
            contexto, NullLogger<RepositorioDeSessaoWhatsApp>.Instance);
        await sessao.GravarAsync("{\"a\":1}", "{\"b\":2}", 0);

        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(w => w.ObterStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ligado());

        var resultado = await CriarController(whatsApp, sessao).Status(CancellationToken.None);

        // Anônimo e internal: dynamic não enxerga. Reflexão, sem expor DTO.
        // Nomes CLR: inferidos de `status.Pareado` vêm com maiúscula; só os
        // explícitos (`sessaoNoBanco = ...`) são camelCase. No JSON todos saem
        // camelCase pela policy da API.
        var corpo = Assert.IsType<OkObjectResult>(resultado).Value!;
        var tipo = corpo.GetType();
        Assert.True((bool)tipo.GetProperty("sessaoNoBanco")!.GetValue(corpo)!);
        Assert.NotNull(tipo.GetProperty("sessaoAtualizadaEm")!.GetValue(corpo));
        Assert.True((bool)tipo.GetProperty("Pareado")!.GetValue(corpo)!);
    }

    [Fact]
    public async Task Status_diz_quando_nao_ha_sessao_guardada()
    {
        await using var contexto = Testes.CriarContextoInMemory();
        var sessao = new RepositorioDeSessaoWhatsApp(
            contexto, NullLogger<RepositorioDeSessaoWhatsApp>.Instance);

        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(w => w.ObterStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Ligado());

        var resultado = await CriarController(whatsApp, sessao).Status(CancellationToken.None);

        var corpo = Assert.IsType<OkObjectResult>(resultado).Value!;
        var tipo = corpo.GetType();
        Assert.False((bool)tipo.GetProperty("sessaoNoBanco")!.GetValue(corpo)!);
        Assert.Null(tipo.GetProperty("sessaoAtualizadaEm")!.GetValue(corpo));
    }
}

public class AdminEntregadoresControllerTests
{
    private static AdminEntregadoresController CriarController(Mock<IGestaoDeEntregadoresService> servico) =>
        new(servico.Object);

    [Fact]
    public async Task Listar_retorna_os_entregadores()
    {
        var servico = new Mock<IGestaoDeEntregadoresService>();
        var lista = new[] { new EntregadorDto(1, "João", "joao", true, DateTime.UtcNow) };
        servico.Setup(s => s.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lista);

        var resultado = await CriarController(servico).Listar(CancellationToken.None);

        Assert.Equal(lista, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Criar_retorna_created_com_o_entregador()
    {
        var servico = new Mock<IGestaoDeEntregadoresService>();
        var entregador = new EntregadorDto(1, "João", "joao", true, DateTime.UtcNow);
        servico.Setup(s => s.CriarAsync(It.IsAny<RequisicaoDeEntregador>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entregador);

        var resultado = await CriarController(servico)
            .Criar(new RequisicaoDeEntregador("João", "joao", "secreta123"), CancellationToken.None);

        Assert.Equal(entregador, Assert.IsType<CreatedAtActionResult>(resultado).Value);
    }

    [Fact]
    public async Task Criar_retorna_bad_request_quando_o_login_ja_existe()
    {
        var servico = new Mock<IGestaoDeEntregadoresService>();
        servico.Setup(s => s.CriarAsync(It.IsAny<RequisicaoDeEntregador>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Já existe um entregador com o usuário joao."));

        var resultado = await CriarController(servico)
            .Criar(new RequisicaoDeEntregador("João", "joao", "secreta123"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(resultado);
    }

    [Fact]
    public async Task Atualizar_retorna_not_found_quando_inexistente()
    {
        var servico = new Mock<IGestaoDeEntregadoresService>();
        servico.Setup(s => s.AtualizarAsync(It.IsAny<long>(), It.IsAny<RequisicaoDeAtualizacaoDeEntregador>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Entregador não encontrado."));

        var resultado = await CriarController(servico)
            .Atualizar(99, new RequisicaoDeAtualizacaoDeEntregador("João", "joao", null, true), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(resultado);
    }
}
