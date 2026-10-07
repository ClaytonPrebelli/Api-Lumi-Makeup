using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeFreteServiceTests
{
    private static readonly ResultadoViaCep DoCep = new(
        "01310300",
        "Avenida Paulista",
        "Bela Vista",
        "SÃ£o Paulo",
        "SP");

    private static Mock<IViaCepService> CepQueResponde()
    {
        var mock = new Mock<IViaCepService>();
        mock.Setup(c => c.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DoCep);
        return mock;
    }

    private static Mock<IGeocodificador> Geocodificador(decimal latitude, decimal longitude)
    {
        var mock = new Mock<IGeocodificador>();
        mock.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((latitude, longitude));
        return mock;
    }

    private static Mock<ICalculoDeFreteService> CalculoDeExemplo(decimal custo, decimal distancia)
    {
        var mock = new Mock<ICalculoDeFreteService>();
        mock.Setup(c => c.CalcularAsync(It.IsAny<EnderecoDeEntregaRequisicao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CalculoDeFreteDto(distancia, custo, 1.2m, "Valor fixo até 8 km"));
        return mock;
    }

    private static GestaoDeFreteService Servico(
        LumiDbContext contexto,
        Mock<IGeocodificador>? nominatim = null,
        Mock<ICalculoDeFreteService>? calculo = null) => new(
            contexto,
            CepQueResponde().Object,
            (nominatim ?? Geocodificador(-23.561414m, -46.6565m)).Object,
            (calculo ?? CalculoDeExemplo(12.40m, 8m)).Object);

    [Fact]
    public async Task ObterAsync_devolve_configuracao_vazia_quando_ainda_nao_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var configuracao = await Servico(contexto).ObterAsync(CancellationToken.None);

        // Vazio Ã© o estado inicial de uma loja abrindo, e a tela mostra o
        // formulÃ¡rio para isso. Devolver erro faria a tela parecer quebrada.
        Assert.Equal(0, configuracao.Id);
        Assert.Equal(string.Empty, configuracao.CepOrigem);
        Assert.Null(configuracao.FreteDeExemplo);
    }

    [Fact]
    public async Task SalvarAsync_grava_o_cep_com_a_coordenada_dele()
    {
        using var contexto = Testes.CriarContextoInMemory();

        await Servico(contexto).SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, 12.00m, 18.00m),
            CancellationToken.None);

        var gravado = await contexto.ConfiguracoesDeFrete.SingleAsync();

        Assert.Equal("01310300", gravado.CepOrigem);
        Assert.Equal(1.20m, gravado.PrecoPorKm);
        Assert.Equal(7.50m, gravado.ValorAte8Km);
        Assert.Equal(12.00m, gravado.ValorAte16Km);
        Assert.Equal(18.00m, gravado.ValorAte25Km);
        Assert.Equal(-23.561414m, gravado.LatitudeOrigem);
    }

    [Fact]
    public async Task SalvarAsync_atualiza_sem_criar_uma_segunda_linha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        await servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, 12.00m, 18.00m), CancellationToken.None);
        await servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01425-001", 2.00m, 8.00m, 13.00m, 19.00m), CancellationToken.None);

        // Duas linhas fariam o cÃ¡lculo usar a mais antiga, e a administradora
        // acharia que a tarifa nova foi salva sem nenhum efeito.
        Assert.Single(await contexto.ConfiguracoesDeFrete.ToListAsync());
        Assert.Equal("01425001", (await contexto.ConfiguracoesDeFrete.SingleAsync()).CepOrigem);
    }

    [Fact]
    public async Task SalvarAsync_devolve_o_exemplo_calculado()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var configuracao = await Servico(contexto).SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, 12.00m, 18.00m),
            CancellationToken.None);

        // "R$ 1,20 por km" nÃ£o diz nada concreto. O exemplo Ã© o que faz a
        // administradora saber se a tarifa estÃ¡ boa.
        Assert.Equal(8m, configuracao.DistanciaDeExemplo);
        Assert.Equal(12.40m, configuracao.FreteDeExemplo);
    }

    [Fact]
    public async Task SalvarAsync_recusa_preco_por_km_zero()
    {
        using var contexto = Testes.CriarContextoInMemory();

        // Frete zero faz a loja entregar de graÃ§a em qualquer distÃ¢ncia.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("01310-300", 0m, 7.50m, 12.00m, 18.00m),
                CancellationToken.None));

        Assert.Contains("quilÃ´metro", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_recusa_faixa_com_valor_negativo()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, -1m, 18.00m),
                CancellationToken.None));

        Assert.Contains("faixa", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_recusa_cep_invalido()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("013", 1.20m, 7.50m, 12.00m, 18.00m),
                CancellationToken.None));

        Assert.Contains("CEP", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_recusa_cep_que_o_viacep_nao_conhece()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var viaCep = new Mock<IViaCepService>();
        viaCep.Setup(c => c.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResultadoViaCep?)null);

        var servico = new GestaoDeFreteService(
            contexto,
            viaCep.Object,
            Geocodificador(-23.561414m, -46.6565m).Object,
            CalculoDeExemplo(12.40m, 8m).Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("99999-999", 1.20m, 7.50m, 12.00m, 18.00m), CancellationToken.None));

        Assert.Contains("CEP", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_avisa_quando_localiza_o_cep_mas_nao_as_coordenadas()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var nominatim = new Mock<IGeocodificador>();
        nominatim.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal Latitude, decimal Longitude)?)null);

        var servico = new GestaoDeFreteService(
            contexto,
            CepQueResponde().Object,
            nominatim.Object,
            CalculoDeExemplo(12.40m, 8m).Object);

        // As duas mensagens sÃ£o diferentes porque a situation Ã©: um Ã© CEP errado,
        // o outro Ã© o serviÃ§o externo instÃ¡vel, e a administradora precisa saber
        // qual dos dois foi para tentar de novo ou corrigir o dado.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, 12.00m, 18.00m), CancellationToken.None));

        Assert.Contains("coordenadas", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_salva_a_tarifa_mesmo_sem_o_exemplo_sair()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var calculo = new Mock<ICalculoDeFreteService>();
        calculo.Setup(c => c.CalcularAsync(It.IsAny<EnderecoDeEntregaRequisicao>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NÃ£o conseguimos localizar esse endereÃ§o."));

        var servico = new GestaoDeFreteService(
            contexto,
            CepQueResponde().Object,
            Geocodificador(-23.561414m, -46.6565m).Object,
            calculo.Object);

        var configuracao = await servico.SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 7.50m, 12.00m, 18.00m),
            CancellationToken.None);

        // O exemplo Ã© ilustrativo. Falhar ele nÃ£o pode impedir a gravaÃ§Ã£o de uma
        // tarifa que pode estar correta.
        Assert.Equal(1.20m, configuracao.PrecoPorKm);
        Assert.Null(configuracao.FreteDeExemplo);
        Assert.Single(await contexto.ConfiguracoesDeFrete.ToListAsync());
    }

    [Fact]
    public async Task SimularAsync_devolve_o_frete_de_um_cep()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var calculo = await servico.SimularAsync("01425-001", CancellationToken.None);

        Assert.Equal(12.40m, calculo.Custo);
    }

    [Fact]
    public async Task SimularAsync_recusa_cep_invalido()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SimularAsync("014", CancellationToken.None));

        Assert.Contains("CEP", erro.Message);
    }
}
