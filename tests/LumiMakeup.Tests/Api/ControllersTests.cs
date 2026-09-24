using System.Security.Claims;
using LumiMakeup.Api.Controllers;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LumiMakeup.Tests.Api;

public class AutenticacaoControllerTests
{
    private static readonly UsuarioDto Usuario = new(42, "Maria", "maria@exemplo.com", null, null, PapelUsuario.Cliente, true, false);

    private static readonly RespostaDeAutenticacao Resposta = new("acesso", "refresh", Usuario);

    private static AutenticacaoController CriarController(
        Mock<IAutenticacaoService> servico,
        string? sub = null)
    {
        var controller = new AutenticacaoController(servico.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (sub is not null)
        {
            var identidade = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, sub) });
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identidade);
        }

        return controller;
    }

    [Fact]
    public async Task Cadastrar_retorna_ok_com_resposta()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.CadastrarAsync(It.IsAny<RequisicaoDeRegistro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resposta);
        var controller = CriarController(servico);

        var resultado = await controller.Cadastrar(new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "Senha@2026!", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(Resposta, ok.Value);
    }

    [Fact]
    public async Task Cadastrar_retorna_conflict_quando_email_ja_existe()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.CadastrarAsync(It.IsAny<RequisicaoDeRegistro>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("E-mail já cadastrado."));
        var controller = CriarController(servico);

        var resultado = await controller.Cadastrar(new RequisicaoDeRegistro("Maria", "maria@exemplo.com", "Senha@2026!", null), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(resultado);
        Assert.Equal("E-mail já cadastrado.", conflict.Value!.GetType().GetProperty("message")!.GetValue(conflict.Value));
    }

    [Fact]
    public async Task Entrar_retorna_ok_com_resposta()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.EntrarAsync(It.IsAny<RequisicaoDeLogin>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resposta);
        var controller = CriarController(servico);

        var resultado = await controller.Entrar(new RequisicaoDeLogin("maria@exemplo.com", "Senha@2026!"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Entrar_retorna_nao_autorizado_quando_credenciais_invalidas()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.EntrarAsync(It.IsAny<RequisicaoDeLogin>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Credenciais inválidas."));
        var controller = CriarController(servico);

        var resultado = await controller.Entrar(new RequisicaoDeLogin("maria@exemplo.com", "errada"), CancellationToken.None);

        var naoAutorizado = Assert.IsType<UnauthorizedObjectResult>(resultado);
        Assert.Equal("Credenciais inválidas.", naoAutorizado.Value!.GetType().GetProperty("message")!.GetValue(naoAutorizado.Value));
    }

    [Fact]
    public async Task EntrarComGoogle_retorna_ok_com_resposta()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.EntrarComGoogleAsync(It.IsAny<RequisicaoDeLoginGoogle>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resposta);
        var controller = CriarController(servico);

        var resultado = await controller.EntrarComGoogle(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task EntrarComGoogle_retorna_nao_autorizado_quando_token_invalido()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.EntrarComGoogleAsync(It.IsAny<RequisicaoDeLoginGoogle>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Token inválido."));
        var controller = CriarController(servico);

        var resultado = await controller.EntrarComGoogle(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(resultado);
    }

    [Fact]
    public async Task EntrarComGoogle_retorna_conflict_quando_email_ja_cadastrado()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.EntrarComGoogleAsync(It.IsAny<RequisicaoDeLoginGoogle>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Já existe uma conta com este e-mail."));
        var controller = CriarController(servico);

        var resultado = await controller.EntrarComGoogle(new RequisicaoDeLoginGoogle("token"), CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(resultado);
    }

    [Fact]
    public async Task Renovar_retorna_ok_com_resposta()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Resposta);
        var controller = CriarController(servico);

        var resultado = await controller.Renovar(new RequisicaoDeRenovacao("refresh"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task Renovar_retorna_nao_autorizado_quando_token_de_refresh_invalido()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Refresh token inválido."));
        var controller = CriarController(servico);

        var resultado = await controller.Renovar(new RequisicaoDeRenovacao("invalido"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(resultado);
    }

    [Fact]
    public async Task ObterUsuarioAtual_retorna_ok_com_usuario_validando_o_claim()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.ObterUsuarioAtualAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usuario);
        var controller = CriarController(servico, sub: "42");

        var resultado = await controller.ObterUsuarioAtual(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(Usuario, ok.Value);
    }

    [Fact]
    public async Task ObterUsuarioAtual_sem_claim_retorna_nao_autorizado()
    {
        var servico = new Mock<IAutenticacaoService>();
        var controller = CriarController(servico);

        var resultado = await controller.ObterUsuarioAtual(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task ObterUsuarioAtual_claim_nao_numerico_retorna_nao_autorizado()
    {
        var servico = new Mock<IAutenticacaoService>();
        var controller = CriarController(servico, sub: "nao-e-numero");

        var resultado = await controller.ObterUsuarioAtual(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task CompletarPerfil_retorna_ok_com_perfil_do_usuario()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.CompletarPerfilAsync(42, It.IsAny<RequisicaoDeCompletarPerfil>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Usuario);
        var controller = CriarController(servico, sub: "42");
        var requisicao = new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, null);

        var resultado = await controller.CompletarPerfil(requisicao, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact]
    public async Task CompletarPerfil_retorna_bad_request_quando_validacao_falhar()
    {
        var servico = new Mock<IAutenticacaoService>();
        servico.Setup(s => s.CompletarPerfilAsync(42, It.IsAny<RequisicaoDeCompletarPerfil>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("CPF inválido."));
        var controller = CriarController(servico, sub: "42");

        var resultado = await controller.CompletarPerfil(new RequisicaoDeCompletarPerfil("Maria", "0", null, null), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("CPF inválido.", badRequest.Value!.GetType().GetProperty("message")!.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task CompletarPerfil_sem_claim_retorna_nao_autorizado()
    {
        var servico = new Mock<IAutenticacaoService>();
        var controller = CriarController(servico);

        var resultado = await controller.CompletarPerfil(new RequisicaoDeCompletarPerfil("Maria", "12345678901", null, null), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }
}

public class SaudeControllerTests
{
    [Fact]
    public void Obter_retorna_ok_com_status_ok()
    {
        var controller = new SaudeController();

        var resultado = controller.Obter();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var valor = ok.Value!.GetType();
        Assert.Equal("ok", valor.GetProperty("status")!.GetValue(ok.Value));
        Assert.NotNull(valor.GetProperty("timestamp")!.GetValue(ok.Value));
    }
}

public class CategoriasControllerTests
{
    [Fact]
    public async Task ObterTodas_retorna_ok_com_as_categorias()
    {
        var catalogo = new Mock<ICatalogoService>();
        var categorias = new CategoriaDto[]
        {
            new(1, "Bases", "bases", "Maquiagem", true),
            new(2, "Batom", "batom", null, true)
        };
        catalogo.Setup(c => c.ObterCategoriasAtivasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categorias);
        var controller = new CategoriasController(catalogo.Object);

        var resultado = await controller.ObterTodas(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(categorias, ok.Value);
    }
}

public class ProdutosControllerTests
{
    private static ProdutoDto CriarProduto()
    {
        return new ProdutoDto(10, "Batom Matte", "batom-matte", "Batom de alta duração", 39.90m, 5, true, 2, "Batom", new ImagemProdutoDto[] { new(1, "https://exemplo.com/batom.jpg", 0) });
    }

    [Fact]
    public async Task ObterTodos_retorna_ok_com_os_produtos()
    {
        var catalogo = new Mock<ICatalogoService>();
        var produtos = new ProdutoDto[] { CriarProduto() };
        catalogo.Setup(c => c.ObterProdutosAtivosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(produtos);
        var controller = new ProdutosController(catalogo.Object);

        var resultado = await controller.ObterTodos(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(produtos, ok.Value);
    }

    [Fact]
    public async Task ObterPorSlug_retorna_ok_com_o_produto()
    {
        var catalogo = new Mock<ICatalogoService>();
        var produto = CriarProduto();
        catalogo.Setup(c => c.ObterProdutoPorSlugAsync("batom-matte", It.IsAny<CancellationToken>()))
            .ReturnsAsync(produto);
        var controller = new ProdutosController(catalogo.Object);

        var resultado = await controller.ObterPorSlug("batom-matte", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(produto, ok.Value);
    }

    [Fact]
    public async Task ObterPorSlug_retorna_not_found_quando_produto_inexistente()
    {
        var catalogo = new Mock<ICatalogoService>();
        catalogo.Setup(c => c.ObterProdutoPorSlugAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProdutoDto?)null);
        var controller = new ProdutosController(catalogo.Object);

        var resultado = await controller.ObterPorSlug("inexistente", CancellationToken.None);

        Assert.IsType<NotFoundResult>(resultado);
    }
}