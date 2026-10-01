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
        decimal taxaMinima = 9.00m)
    {
        contexto.ConfiguracoesDeFrete.Add(new ConfiguracaoFrete
        {
            CepOrigem = "01310-930",
            LatitudeOrigem = LatPaulista,
            LongitudeOrigem = LonPaulista,
            PrecoPorKm = precoPorKm,
            TaxaMinima = taxaMinima
        });

        await contexto.SaveChangesAsync();
    }

    private static Mock<INominatimService> Geocodificador(decimal latitude, decimal longitude)
    {
        var mock = new Mock<INominatimService>();
        mock.Setup(n => n.GeocodificarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((latitude, longitude));
        return mock;
    }

    // ----Calculo ----------------------------------------------------------

    [Fact]
    public async Task CalcularAsync_aplica_o_preco_por_km()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, precoPorKm: 1.20m, taxaMinima: 0m);

        // 0,1 grau de longitude na latitude de São Paulo é ~10,6 km de linha reta,
        // que viram ~13,8 pelo fator de rota urbana. O valor exato depende da
        // geometria da esfera, e o que o teste segura é a conta, não o arredondamento.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.1m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.Equal(13.25m, resultado.DistanciaKm);
        Assert.Equal(15.90m, resultado.Custo);
    }

    [Fact]
    public async Task CalcularAsync_respeita_a_taxa_minima_em_endereco_proximo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, precoPorKm: 1.20m, taxaMinima: 9.00m);

        // 300 m de distância dariam R$ 0,47 pelo preço por km. Pagar esse valor
        // por uma remessa de 300 metros não cobre o custo de empacotar.
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.003m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        Assert.Equal(9.00m, resultado.Custo);
    }

    [Fact]
    public async Task CalcularAsync_devolve_a_tarifa_para_a_tela_explicar_o_numero()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearConfiguracaoAsync(contexto, precoPorKm: 1.20m, taxaMinima: 9.00m);
        var mock = Geocodificador(LatPaulista, LonPaulista + 0.1m);
        var servico = new CalculoDeFreteService(contexto, mock.Object);

        var resultado = await servico.CalcularAsync(Destino(), CancellationToken.None);

        // Sem o preço por km e a taxa mínima no corpo, o checkout mostraria um
        // total que ninguém entende de onde saiu.
        Assert.Equal(1.20m, resultado.PrecoPorKm);
        Assert.Equal(9.00m, resultado.TaxaMinima);
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

        var mock = new Mock<INominatimService>();
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
        var mock = new Mock<INominatimService>();
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
