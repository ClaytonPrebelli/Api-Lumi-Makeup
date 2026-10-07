using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class CalculoDeFreteServiceTests
{
    /// <summary>Paulista para Paulista: mesma avenida, poucos metros de diferença.</summary>
    private const decimal LatPaulista = -23.561414m;
    private const decimal LonPaulista = -46.6565m;

    private static EnderecoDeEntregaRequisicao Destino(
        string cep = "01310930",
        string logradouro = "Avenida Paulista",
        string numero = "1000",
        string bairro = "Bela Vista",
        string cidade = "São Paulo",
        string estado = "SP") => new(cep, logradouro, numero, null, bairro, cidade, estado);

    private static async Task SemearConfiguracaoAsync(
        LumiDbContext contexto,
        decimal precoPorKm = 1.20m,
        decimal valorAte8Km = 7.50m,
        decimal valorAte16Km = 12.00m,
        decimal valorAte25Km = 18.00m)
    {
        contexto.ConfiguracoesDeFrete.Add(new ConfiguracaoFrete
        {
            CepOrigem = "01310-930",
            LatitudeOrigem = LatPaulista,
            LongitudeOrigem = LonPaulista,
            PrecoPorKm = precoPorKm,
            ValorAte8Km = valorAte8Km,
            ValorAte16Km = valorAte16Km,
            ValorAte25Km = valorAte25Km
        });

        await contexto.SaveChangesAsync();
    }

    private static Mock<IGeocodificador> Geocodificador(decimal latitude, decimal longitude)
    {
        var mock = new Mock<IGeocodificador>();
        mock.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((latitude, longitude));
        return mock;
    }

    // ----Calculo ----------------------------------------------------------

    [Fact]
    public async Task CalcularAsync_cobra_por_km_acima_de_25_km()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, precoPorKm: 1.20m);

        // 0,2 grau de longitude dá mais de 25 km com o fator de rota. Acima de
        // 25 km não há valor fixo: volta a ser preço por quilômetro.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.2m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.True(resultado.DistanciaKm > 25m);
        Assert.Equal(Math.Round(resultado.DistanciaKm * 1.20m, 2), resultado.Custo);
        Assert.Null(resultado.DescricaoFaixa);
    }

    [Fact]
    public async Task CalcularAsync_cobra_valor_fixo_ate_8_km()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, valorAte8Km: 7.50m);

        // 300 m de distância dariam centavos pelo preço por km. Perto, o frete
        // é o fixo da primeira faixa.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.003m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.Equal(7.50m, resultado.Custo);
        Assert.Equal("Valor fixo até 8 km", resultado.DescricaoFaixa);
    }

    [Fact]
    public async Task CalcularAsync_cobra_valor_fixo_de_8_a_16_km()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, valorAte16Km: 12.00m);

        // 0,1 grau dá 13,25 km com o fator de rota: segunda faixa.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.1m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.Equal(13.25m, resultado.DistanciaKm);
        Assert.Equal(12.00m, resultado.Custo);
        Assert.Equal("Valor fixo de 8 a 16 km", resultado.DescricaoFaixa);
    }

    [Fact]
    public async Task CalcularAsync_cobra_valor_fixo_de_16_a_25_km()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, valorAte25Km: 18.00m);

        // 0,15 grau dá cerca de 19,9 km com o fator de rota: terceira faixa.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.15m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.InRange(resultado.DistanciaKm, 16m, 25m);
        Assert.Equal(18.00m, resultado.Custo);
        Assert.Equal("Valor fixo de 16 a 25 km", resultado.DescricaoFaixa);
    }

    [Fact]
    public async Task CalcularAsync_devolve_a_tarifa_para_a_tela_explicar_o_numero()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, precoPorKm: 1.20m);
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.1m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        // Sem o preço por km e a descrição da faixa no corpo, o checkout
        // mostraria um total que ninguém entende de onde saiu.
        Assert.Equal(1.20m, resultado.PrecoPorKm);
        Assert.Equal("Valor fixo de 8 a 16 km", resultado.DescricaoFaixa);
    }

    [Fact]
    public async Task CalcularAsync_falha_quando_a_loja_nao_tem_frete_configurado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var mock = Geocodificador(LatPaulista, LonPaulista);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        // Sem a tabela preenchida não existe tarifa, e inventar uma daria um
        // número no total que ninguém saberia explicar depois.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync(Destino(), CancellationToken.None));

        Assert.Contains("frete", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_falha_quando_o_endereco_nao_e_localizado()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto);

        var mock = new Mock<IGeocodificador>();
        mock.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((decimal Latitude, decimal Longitude)?)null);

        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync(Destino(), CancellationToken.None));

        Assert.Contains("endereço", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_recusa_endereco_longe_demais()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto);

        // Manaus: mais de 3.000 km de linha reta. Acima do limite a distância já
        // é estimativa grosseira, e cobrar por ela seria cobrar errado.
        var mock = Geocodificador(-3.1190m, -60.0217m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync(
                Destino(cep: "69000-000", logradouro: "Rua A", numero: "1", bairro: "Centro", cidade: "Manaus", estado: "AM"),
                CancellationToken.None));

        Assert.Contains("longe", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_manda_o_endereco_completo_para_geocodificar()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto);

        string? consultado = null;
        var mock = new Mock<IGeocodificador>();
        mock.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string consulta, CancellationToken _) =>
            {
                consultado = consulta;
                return (LatPaulista, LonPaulista);
            });

        var servico = new CalculoDeFreteService(contexto, mock.Object);
        await servico.CalcularAsync(
            Destino(cep: "01310-930", numero: "1500", bairro: "Jardim Paulista"),
            CancellationToken.None);

        // Sem o número, o geocodificador aponta para o meio da rua e a distância
        // sai errada por centenas de metros.
        Assert.NotNull(consultado);
        Assert.Contains("01310-930", consultado);
        Assert.Contains("1500", consultado);
        Assert.Contains("Jardim Paulista", consultado);
    }

    // ----Distancia --------------------------------------------------------

    [Fact]
    public void DistanciaEmKm_zer_no_mesmo_ponto()
    {
        Assert.Equal(0m, CalculoDeFreteService.DistanciaEmKm(LatPaulista, LonPaulista, LatPaulista, LonPaulista));
    }

    [Fact]
    public void DistanciaEmKm_conhece_a_distancia_real_entre_cidades()
    {
        // São Paulo e Rio são ~360 km de linha reta. A fórmula de Haversine dá
        // esse valor; a conta errada que trata grau como metro daria 10.000 km.
        var distancia = CalculoDeFreteService.DistanciaEmKm(
            -23.5505m, -46.6333m,
            -22.9068m, -43.1729m);

        Assert.InRange(distancia, 350m, 370m);
    }

    [Fact]
    public void DistanciaEmKm_calcula_meia_distancia_entre_dois_pontos()
    {
        var ida = CalculoDeFreteService.DistanciaEmKm(0m, 0m, 0m, 1m);
        var volta = CalculoDeFreteService.DistanciaEmKm(0m, 1m, 0m, 0m);

        Assert.Equal(ida, volta);
    }
}
