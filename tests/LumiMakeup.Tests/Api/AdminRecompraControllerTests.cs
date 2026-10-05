using LumiMakeup.Api.Controllers;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LumiMakeup.Tests.Api;

public class AdminRecompraControllerTests
{
    [Fact]
    public async Task Obter_retorna_configuracao()
    {
        var service = new Mock<IRecompraService>();
        var configuracao = new ConfiguracaoDeRecompraDto(true, "Assunto", "Mensagem", DateTime.UtcNow);
        service.Setup(s => s.ObterConfiguracaoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(configuracao);
        var controller = new AdminRecompraController(service.Object);

        var resultado = await controller.ObterConfiguracao(CancellationToken.None);

        Assert.Equal(configuracao, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task Salvar_mapeia_validacao_para_bad_request()
    {
        var service = new Mock<IRecompraService>();
        service.Setup(s => s.SalvarConfiguracaoAsync(
                It.IsAny<RequisicaoDeConfiguracaoDeRecompra>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Informe o assunto do e-mail."));
        var controller = new AdminRecompraController(service.Object);

        var resultado = await controller.SalvarConfiguracao(
            new RequisicaoDeConfiguracaoDeRecompra(true, "", "Mensagem"),
            CancellationToken.None);

        var erro = Assert.IsType<BadRequestObjectResult>(resultado);
        Assert.Equal("Informe o assunto do e-mail.", erro.Value!.GetType().GetProperty("message")!.GetValue(erro.Value));
    }

    [Fact]
    public async Task Disparar_retorna_quantidade_confirmada()
    {
        var service = new Mock<IRecompraService>();
        var resposta = new ResultadoDeDisparoDeRecompraDto(3);
        service.Setup(s => s.DispararAsync(It.IsAny<CancellationToken>())).ReturnsAsync(resposta);
        var controller = new AdminRecompraController(service.Object);

        var resultado = await controller.Disparar(CancellationToken.None);

        Assert.Equal(resposta, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public void Controller_exige_politica_de_administrador()
    {
        var autorizado = Assert.Single(typeof(AdminRecompraController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("SomenteAdministrador", autorizado.Policy);
    }

    [Fact]
    public void Mutacoes_sao_post()
    {
        var configuracao = typeof(AdminRecompraController).GetMethod(nameof(AdminRecompraController.SalvarConfiguracao))!;
        var disparo = typeof(AdminRecompraController).GetMethod(nameof(AdminRecompraController.Disparar))!;

        Assert.NotNull(configuracao.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(disparo.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
    }
}
