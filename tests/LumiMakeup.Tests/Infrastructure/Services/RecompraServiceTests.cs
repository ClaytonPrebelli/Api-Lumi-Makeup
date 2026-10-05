using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public class RecompraServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 4, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Disparar_seleciona_apenas_pedidos_pagos_na_janela_sem_compra_posterior()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var envio = CriarEnviador(true);
        PrepararConfiguracaoAtiva(contexto);
        contexto.Usuarios.AddRange(Enumerable.Range(1, 7).Select(id => Usuario(id)));
        contexto.Pedidos.AddRange(
            Pedido(1, 1, -30),
            Pedido(2, 2, -40),
            Pedido(3, 3, -29),
            Pedido(4, 4, -41),
            Pedido(5, 5, -35),
            Pedido(6, 5, -5, StatusPedido.AguardandoPagamento),
            Pedido(7, 6, -35, StatusPedido.AguardandoPagamento),
            Pedido(8, 7, -35),
            Pedido(9, 7, -20));
        await contexto.SaveChangesAsync();
        var service = CriarService(contexto, envio.Object);

        var resultado = await service.DispararAsync();

        Assert.Equal(2, resultado.Enviados);
        envio.Verify(e => e.EnviarComConfirmacaoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Disparar_nao_repete_envio_confirmado_para_o_mesmo_pedido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var envio = CriarEnviador(true);
        PrepararConfiguracaoAtiva(contexto);
        contexto.Usuarios.Add(Usuario(1));
        contexto.Pedidos.Add(Pedido(10, 1, -35));
        await contexto.SaveChangesAsync();
        var service = CriarService(contexto, envio.Object);

        Assert.Equal(1, (await service.DispararAsync()).Enviados);
        Assert.Equal(0, (await service.DispararAsync()).Enviados);
        Assert.Equal(Agora.UtcDateTime, contexto.EnviosDeRecompra.Single().EnviadoEm);
        envio.Verify(e => e.EnviarComConfirmacaoAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disparar_desabilitado_nao_tenta_enviar()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var envio = CriarEnviador(true);
        contexto.ConfiguracoesDeRecompra.Add(new ConfiguracaoDeRecompra
        {
            Ativa = false,
            Assunto = "Assunto",
            Mensagem = "Mensagem"
        });
        contexto.Usuarios.Add(Usuario(1));
        contexto.Pedidos.Add(Pedido(10, 1, -35));
        await contexto.SaveChangesAsync();
        var service = CriarService(contexto, envio.Object);

        var resultado = await service.DispararAsync();

        Assert.Equal(0, resultado.Enviados);
        envio.VerifyNoOtherCalls();
        Assert.Empty(contexto.EnviosDeRecompra);
    }

    [Fact]
    public async Task Disparar_nao_conta_falha_e_permite_tentar_de_novo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var envio = new Mock<IEmailSenderComConfirmacao>();
        envio.SetupSequence(e => e.EnviarComConfirmacaoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        PrepararConfiguracaoAtiva(contexto);
        contexto.Usuarios.Add(Usuario(1));
        contexto.Pedidos.Add(Pedido(10, 1, -35));
        await contexto.SaveChangesAsync();
        var service = CriarService(contexto, envio.Object);

        Assert.Equal(0, (await service.DispararAsync()).Enviados);
        Assert.Empty(contexto.EnviosDeRecompra);
        Assert.Equal(1, (await service.DispararAsync()).Enviados);
    }

    [Fact]
    public async Task Disparar_nao_conta_excecao_do_enviador_e_libera_pedido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var envio = new Mock<IEmailSenderComConfirmacao>();
        envio.SetupSequence(e => e.EnviarComConfirmacaoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP indisponível"))
            .ReturnsAsync(true);
        PrepararConfiguracaoAtiva(contexto);
        contexto.Usuarios.Add(Usuario(1));
        contexto.Pedidos.Add(Pedido(10, 1, -35));
        await contexto.SaveChangesAsync();
        var service = CriarService(contexto, envio.Object);

        Assert.Equal(0, (await service.DispararAsync()).Enviados);
        Assert.Empty(contexto.EnviosDeRecompra);
        Assert.Equal(1, (await service.DispararAsync()).Enviados);
    }

    [Fact]
    public async Task Salvar_configuracao_rejeita_assunto_ou_mensagem_vazios()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var service = CriarService(contexto, CriarEnviador(true).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarConfiguracaoAsync(new RequisicaoDeConfiguracaoDeRecompra(true, " ", "Mensagem")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarConfiguracaoAsync(new RequisicaoDeConfiguracaoDeRecompra(true, "Assunto", " ")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SalvarConfiguracaoAsync(new RequisicaoDeConfiguracaoDeRecompra(true, new string('a', 201), "Mensagem")));
    }

    [Fact]
    public async Task Configuracao_inicial_e_vazia_e_salvar_persiste_data_utc()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var service = CriarService(contexto, CriarEnviador(true).Object);

        var inicial = await service.ObterConfiguracaoAsync();
        var salva = await service.SalvarConfiguracaoAsync(
            new RequisicaoDeConfiguracaoDeRecompra(true, " Volte em breve ", "<p>Oi</p>"));

        Assert.False(inicial.Ativa);
        Assert.Null(inicial.AtualizadoEm);
        Assert.Equal("Volte em breve", salva.Assunto);
        Assert.Equal("<p>Oi</p>", salva.Mensagem);
        Assert.Equal(Agora.UtcDateTime, salva.AtualizadoEm);
    }

    private static RecompraService CriarService(LumiMakeup.Infrastructure.Persistence.LumiDbContext contexto, IEmailSenderComConfirmacao envio) =>
        new(contexto, envio, new RelogioFixo(Agora), NullLogger<RecompraService>.Instance);

    private static Mock<IEmailSenderComConfirmacao> CriarEnviador(bool resultado)
    {
        var envio = new Mock<IEmailSenderComConfirmacao>();
        envio.Setup(e => e.EnviarComConfirmacaoAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultado);
        return envio;
    }

    private static void PrepararConfiguracaoAtiva(LumiMakeup.Infrastructure.Persistence.LumiDbContext contexto) =>
        contexto.ConfiguracoesDeRecompra.Add(new ConfiguracaoDeRecompra
        {
            Ativa = true,
            Assunto = "Sentimos sua falta",
            Mensagem = "<p>Volte!</p>",
            AtualizadoEm = Agora.UtcDateTime
        });

    private static Usuario Usuario(long id) => new()
    {
        Id = id,
        Nome = $"Cliente {id}",
        Email = $"cliente{id}@exemplo.com"
    };

    private static Pedido Pedido(long id, long usuarioId, int diasAtras, StatusPedido status = StatusPedido.Pago)
    {
        var pagoEm = Agora.UtcDateTime.AddDays(diasAtras);
        return new Pedido
        {
            Id = id,
            UsuarioId = usuarioId,
            NomeCliente = $"Cliente {usuarioId}",
            EmailContato = $"cliente{usuarioId}@exemplo.com",
            Status = status,
            PagoEm = status == StatusPedido.Pago ? pagoEm : null,
            CriadoEm = pagoEm
        };
    }

    private sealed class RelogioFixo(DateTimeOffset instante) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instante;
    }
}
