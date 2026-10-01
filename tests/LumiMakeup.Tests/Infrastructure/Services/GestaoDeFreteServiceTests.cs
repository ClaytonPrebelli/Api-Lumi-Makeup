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
        "São Paulo",
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
            .ReturnsAsync(new CalculoDeFreteDto(distancia, custo, 1.2m, 9m));
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

        // Vazio é o estado inicial de uma loja abrindo, e a tela mostra o
        // formulário para isso. Devolver erro faria a tela parecer quebrada.
        Assert.Equal(0, configuracao.Id);
        Assert.Equal(string.Empty, configuracao.CepOrigem);
        Assert.Null(configuracao.FreteDeExemplo);
    }

    [Fact]
    public async Task SalvarAsync_grava_o_cep_com_a_coordenada_dele()
    {
        using var contexto = Testes.CriarContextoInMemory();

        await Servico(contexto).SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 9.00m),
            CancellationToken.None);

        var gravado = await contexto.ConfiguracoesDeFrete.SingleAsync();

        Assert.Equal("01310300", gravado.CepOrigem);
        Assert.Equal(1.20m, gravado.PrecoPorKm);
        Assert.Equal(9.00m, gravado.TaxaMinima);
        Assert.Equal(-23.561414m, gravado.LatitudeOrigem);
    }

    [Fact]
    public async Task SalvarAsync_atualiza_sem_criar_uma_segunda_linha()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        await servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 9.00m), CancellationToken.None);
        await servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01425-001", 2.00m, 12.00m), CancellationToken.None);

        // Duas linhas fariam o cálculo usar a mais antiga, e a administradora
        // acharia que a tarifa nova foi salva sem nenhum efeito.
        Assert.Single(await contexto.ConfiguracoesDeFrete.ToListAsync());
        Assert.Equal("01425001", (await contexto.ConfiguracoesDeFrete.SingleAsync()).CepOrigem);
    }

    [Fact]
    public async Task SalvarAsync_devolve_o_exemplo_calculado()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var configuracao = await Servico(contexto).SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 9.00m),
            CancellationToken.None);

        // "R$ 1,20 por km" não diz nada concreto. O exemplo é o que faz a
        // administradora saber se a tarifa está boa.
        Assert.Equal(8m, configuracao.DistanciaDeExemplo);
        Assert.Equal(12.40m, configuracao.FreteDeExemplo);
    }

    [Fact]
    public async Task SalvarAsync_calcula_o_exemplo_perto_da_loja_e_nao_em_cidade_fixa()
    {
        using var contexto = Testes.CriarContextoInMemory();

        // O exemplo é o cartão "perto da loja". Com CEPs fixos de São Paulo, uma
        // loja de Sorocaba veria a distância de São Paulo - e acreditaria que a
        // tarifa está cobrando o valor errado.
        var viaCep = new Mock<IViaCepService>();
        var pedidos = new List<string>();

        viaCep.Setup(c => c.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cep, CancellationToken _) =>
            {
                pedidos.Add(cep);

                return new ResultadoViaCep(
                    cep,
                    "Rua Rosalina Ribeiro",
                    "Jardim Golden Park",
                    "Sorocaba",
                    "SP");
            });

        var enderecoVisto = (EnderecoDeEntregaRequisicao?)null;
        var calculo = new Mock<ICalculoDeFreteService>();
        calculo
            .Setup(c => c.CalcularAsync(It.IsAny<EnderecoDeEntregaRequisicao>(), It.IsAny<CancellationToken>()))
            .Callback<EnderecoDeEntregaRequisicao, CancellationToken>((e, _) => enderecoVisto = e)
            .ReturnsAsync(new CalculoDeFreteDto(8.61m, 12.40m, 1.2m, 9m));

        var servico = new GestaoDeFreteService(
            contexto,
            viaCep.Object,
            Geocodificador(-23.4445647m, -47.5098902m).Object,
            calculo.Object);

        await servico.SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("18072-759", 1.20m, 9.00m),
            CancellationToken.None);

        // O CEP vizinho tem que ser da cidade da loja, nunca de uma cidade fixa.
        Assert.NotNull(enderecoVisto);
        Assert.Equal("Sorocaba", enderecoVisto!.Cidade);
        Assert.Equal("SP", enderecoVisto.Estado);

        // O CEP é montado com 8 dígitos. Um CEP de 7 viraria "1807--000" na
        // consulta, que o geocodificador não encontra.
        Assert.Equal(8, enderecoVisto.Cep.Where(char.IsDigit).Count());
        Assert.Equal(9, enderecoVisto.Cep.Length);
        Assert.StartsWith("1807", enderecoVisto.Cep);
        Assert.All(pedidos, cep => Assert.Equal(8, cep.Length));
    }

    [Fact]
    public async Task SalvarAsync_recusa_preco_por_km_zero()
    {
        using var contexto = Testes.CriarContextoInMemory();

        // Frete zero faz a loja entregar de graça em qualquer distância.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("01310-300", 0m, 9.00m),
                CancellationToken.None));

        Assert.Contains("quilômetro", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_recusa_taxa_minima_negativa()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, -1m),
                CancellationToken.None));

        Assert.Contains("taxa mínima", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_recusa_cep_invalido()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Servico(contexto).SalvarAsync(
                new RequisicaoDeConfiguracaoDeFrete("013", 1.20m, 9.00m),
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
            servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("99999-999", 1.20m, 9.00m), CancellationToken.None));

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

        // As duas mensagens são diferentes porque a situation é: um é CEP errado,
        // o outro é o serviço externo instável, e a administradora precisa saber
        // qual dos dois foi para tentar de novo ou corrigir o dado.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.SalvarAsync(new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 9.00m), CancellationToken.None));

        Assert.Contains("coordenadas", erro.Message);
    }

    [Fact]
    public async Task SalvarAsync_salva_a_tarifa_mesmo_sem_o_exemplo_sair()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var calculo = new Mock<ICalculoDeFreteService>();
        calculo.Setup(c => c.CalcularAsync(It.IsAny<EnderecoDeEntregaRequisicao>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Não conseguimos localizar esse endereço."));

        var servico = new GestaoDeFreteService(
            contexto,
            CepQueResponde().Object,
            Geocodificador(-23.561414m, -46.6565m).Object,
            calculo.Object);

        var configuracao = await servico.SalvarAsync(
            new RequisicaoDeConfiguracaoDeFrete("01310-300", 1.20m, 9.00m),
            CancellationToken.None);

        // O exemplo é ilustrativo. Falhar ele não pode impedir a gravação de uma
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
    public async Task SimularAsync_completa_o_endereco_com_o_cep_antes_de_calcular()
    {
        // A simulação recebia só o CEP, e o cálculo montava a consulta de
        // geocodificação com o logradouro, o número, o bairro, a cidade e o
        // estado vazios. O geocodificador não acha um endereço sem cidade, e a
        // tela recusava com "não conseguimos localizar esse endereço".
        using var contexto = Testes.CriarContextoInMemory();

        EnderecoDeEntregaRequisicao? recebido = null;

        var calculo = new Mock<ICalculoDeFreteService>();
        calculo
            .Setup(c => c.CalcularAsync(It.IsAny<EnderecoDeEntregaRequisicao>(), It.IsAny<CancellationToken>()))
            .Callback<EnderecoDeEntregaRequisicao, CancellationToken>((e, _) => recebido = e)
            .ReturnsAsync(new CalculoDeFreteDto(1m, 10m, 1.2m, 9m));

        var servico = new GestaoDeFreteService(
            contexto,
            CepQueResponde().Object,
            Geocodificador(1m, 1m).Object,
            calculo.Object);

        await servico.SimularAsync("01310-300", CancellationToken.None);

        Assert.NotNull(recebido);
        Assert.Equal("01310-300", recebido!.Cep);
        Assert.Equal("Avenida Paulista", recebido.Logradouro);
        Assert.Equal("São Paulo", recebido.Cidade);
        Assert.Equal("SP", recebido.Estado);
    }

    [Fact]
    public void a_consulta_de_geocodificacao_tem_a_cidade_e_nao_o_bairro_no_lugar()
    {
        // A ordem dos campos na montagem importa mais do que parece: se a cidade
        // e o estado sairem fora de ordem, o geocodificador deixa de achar o
        // lugar e a tela recusa o calculo.
        var consulta = CalculoDeFreteService.MontarConsultaDeGeocodificacao(
            new EnderecoDeEntregaRequisicao(
                "18080-001",
                "Rua Comendador Hermelino Matarazzo",
                string.Empty,
                null,
                "Vila Santa Rita",
                "Sorocaba",
                "SP"));

        Assert.Contains("Sorocaba", consulta);
        Assert.Contains("SP", consulta);
        Assert.DoesNotContain(", ,", consulta);
        Assert.DoesNotContain("\"", consulta);
    }

    [Fact]
    public async Task SimularAsync_avisa_quando_o_cep_nao_existe()
    {
        using var contexto = Testes.CriarContextoInMemory();

        var cepVazio = new Mock<IViaCepService>();
        cepVazio
            .Setup(c => c.ConsultarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResultadoViaCep?)null);

        var servico = new GestaoDeFreteService(
            contexto,
            cepVazio.Object,
            Geocodificador(1m, 1m).Object,
            CalculoDeExemplo(10m, 1m).Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.SimularAsync("99999-999", CancellationToken.None));

        // Sem essa mensagem, a tela culpa o geocodificador por um CEP que
        // simplesmente não existe.
        Assert.Contains("CEP", erro.Message);
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
