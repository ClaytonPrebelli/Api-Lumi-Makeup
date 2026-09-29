using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeCuponsServiceTests
{
    private static async Task<Cupom> SemearAsync(
        LumiDbContext contexto,
        string codigo = "NATAL20",
        decimal percentual = 20,
        int quantidade = 10,
        decimal valorMinimo = 0,
        DateTime? validade = null,
        bool ativo = true)
    {
        var cupom = new Cupom
        {
            Codigo = codigo,
            Percentual = percentual,
            QuantidadeDisponivel = quantidade,
            ValorMinimo = valorMinimo,
            ValidadeAte = validade,
            Ativo = ativo
        };

        contexto.Cupons.Add(cupom);
        await contexto.SaveChangesAsync();
        return cupom;
    }

    private static GestaoDeCuponsService Servico(LumiDbContext contexto) => new(contexto);

    // ----Criacao e edicao -------------------------------------------------

    [Fact]
    public async Task CriarAsync_normaliza_o_codigo_para_maiusculo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var cupom = await servico.CriarAsync(
            Requisicao("natal20", 20, 10, 0, null, true),
            CancellationToken.None);

        Assert.Equal("NATAL20", cupom.Codigo);
        Assert.Single(contexto.Cupons.ToList());
    }

    [Fact]
    public async Task CriarAsync_tira_acentos_e_espacos_do_codigo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        // Campanha digitada por alguém no celular, sem acertar o teclado.
        var cupom = await servico.CriarAsync(
            Requisicao(" dia das mães ", 20, 10, 0, null, true),
            CancellationToken.None);

        // O til do "a" e marca combinante e some na normalizacao, sobrando o "a".
        Assert.Equal("DIADASMAES", cupom.Codigo);
    }

    [Fact]
    public async Task CriarAsync_recusa_codigo_repetido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await servico.CriarAsync(Requisicao("NATAL20", 20, 10, 0, null, true), CancellationToken.None);

        // "natal20" e "NATAL20" sao o mesmo cupom: o codigo e gravado normalizado.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao("natal20", 30, 5, 0, null, true), CancellationToken.None));

        Assert.Contains("NATAL20", erro.Message);
        Assert.Single(contexto.Cupons.ToList());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public async Task CriarAsync_recusa_percentual_fora_de_zero_a_cem(decimal percentual)
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao("X", percentual, 10, 0, null, true), CancellationToken.None));
    }

    [Fact]
    public async Task CriarAsync_aceita_percentual_cheio_de_cem()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var cupom = await servico.CriarAsync(
            Requisicao("ZERA", 100, 5, 0, null, true),
            CancellationToken.None);

        Assert.Equal(100, cupom.Percentual);
    }

    [Fact]
    public async Task CriarAsync_recusa_codigo_sem_letra_nem_numero()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarAsync(Requisicao("   ", 20, 10, 0, null, true), CancellationToken.None));

        Assert.Contains("código", erro.Message);
    }

    [Fact]
    public async Task AtualizarAsync_recusa_ficar_com_o_codigo_de_outro_cupom()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        await SemearAsync(contexto, "ANTIGO");
        var alvo = await SemearAsync(contexto, "ALVO");

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.AtualizarAsync(alvo.Id, Requisicao("ANTIGO", 30, 5, 0, null, true), CancellationToken.None));

        Assert.Contains("ANTIGO", erro.Message);
    }

    [Fact]
    public async Task AtualizarAsync_aceita_manter_o_proprio_codigo()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);
        var alvo = await SemearAsync(contexto, "ALVO");

        // Reaproveitar o codigo que o proprio cupom ja usa tem de funcionar: senao
        // nao daria para mexer no percentual sem trocar o codigo junto.
        var atualizado = await servico.AtualizarAsync(
            alvo.Id,
            Requisicao("ALVO", 30, 5, 0, null, true),
            CancellationToken.None);

        Assert.Equal("ALVO", atualizado.Codigo);
        Assert.Equal(30, atualizado.Percentual);
    }

    [Fact]
    public async Task SomarQuantidadeAsync_acrescenta_sem_apagar_o_uso()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto, quantidade: 2);
        var servico = Servico(contexto);

        var somado = await servico.SomarQuantidadeAsync(cupom.Id, 10, CancellationToken.None);

        // Somar e o que reponho o estoque quando o cupom esgota. Reescrever a
        // quantidade absoluta aqui apagaria os 2 usos ja consumidos.
        Assert.Equal(12, somado.QuantidadeDisponivel);
    }

    [Fact]
    public async Task SomarQuantidadeAsync_recusa_quantidade_nao_positiva()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto);
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.SomarQuantidadeAsync(cupom.Id, 0, CancellationToken.None));
    }

    [Fact]
    public async Task DefinirAtivoAsync_desliga_sem_apagar_a_quantidade()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto, quantidade: 7, ativo: true);
        var servico = Servico(contexto);

        var desligado = await servico.DefinirAtivoAsync(cupom.Id, false, CancellationToken.None);

        // Desligar e temporario: o estoque de usos continua intacto para quando
        // a campanha voltar.
        Assert.False(desligado.Ativo);
        Assert.Equal(7, desligado.QuantidadeDisponivel);
    }

    // ----Calculo do desconto ---------------------------------------------

    [Fact]
    public async Task CalcularAsync_desconta_o_percentual_sobre_o_subtotal()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", percentual: 20);
        var servico = Servico(contexto);

        var aplicacao = await servico.CalcularAsync("NATAL20", 150m, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(30m, aplicacao.Desconto);
        Assert.Equal(20m, aplicacao.Percentual);
        Assert.Equal("NATAL20", aplicacao.Codigo);
    }

    [Fact]
    public async Task CalcularAsync_aceita_o_codigo_em_caixa_baixa_e_com_espaco()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", percentual: 10);
        var servico = Servico(contexto);

        // Recusar "natal20" de um cupom valido seria a falha mais irritante de um
        // campo de texto: parece o sistema estar errado.
        var aplicacao = await servico.CalcularAsync("  natal20 ", 100m, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(10m, aplicacao.Desconto);
    }

    [Fact]
    public async Task CalcularAsync_arredonda_o_centavo_para_cima()
    {
        using var contexto = Testes.CriarContextoInMemory();
        // 33% de 10,00 da 3,30. Com 50% de 0,01 daria 0,005, que arredondado
        // para baixo daria desconto zero num cupom que existe.
        await SemearAsync(contexto, "METADE", percentual: 50);
        var servico = Servico(contexto);

        var aplicacao = await servico.CalcularAsync("METADE", 0.01m, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(0.01m, aplicacao.Desconto);
    }

    [Fact]
    public async Task CalcularAsync_recusa_codigo_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync("FANTASMA", 100m, DateTime.UtcNow, CancellationToken.None));

        Assert.Equal("Cupom inválido.", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_recusa_cupom_desativado_com_a_mesma_mensagem_do_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", ativo: false);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync("NATAL20", 100m, DateTime.UtcNow, CancellationToken.None));

        // Desativado, esgotado e inexistente sao a mesma coisa para quem compra.
        // Dizer qual deles foi entregaria informação de negócio de graça.
        Assert.Equal("Cupom inválido.", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_recusa_cupom_esgotado_com_a_mesma_mensagem()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", quantidade: 0);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync("NATAL20", 100m, DateTime.UtcNow, CancellationToken.None));

        Assert.Equal("Cupom inválido.", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_recusa_cupom_vencido()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", validade: new DateTime(2020, 1, 1));
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync("NATAL20", 100m, new DateTime(2026, 5, 1), CancellationToken.None));

        Assert.Equal("Cupom expirado.", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_aceita_cupom_no_dia_da_validade()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var validade = new DateTime(2026, 5, 1, 23, 59, 0);
        await SemearAsync(contexto, "NATAL20", validade: validade);
        var servico = Servico(contexto);

        // Cortar o cupom no dia inteiro, e nao na meia-noite, evita o coupon
        // "expirado" numa compra feita as 22h do ultimo dia.
        var aplicacao = await servico.CalcularAsync("NATAL20", 100m, validade, CancellationToken.None);

        Assert.Equal(20m, aplicacao.Desconto);
    }

    [Fact]
    public async Task CalcularAsync_recusa_subtotal_abaixo_do_minimo_e_diz_o_valor()
    {
        using var contexto = Testes.CriarContextoInMemory();
        await SemearAsync(contexto, "NATAL20", valorMinimo: 200m);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CalcularAsync("NATAL20", 150m, DateTime.UtcNow, CancellationToken.None));

        Assert.Contains("200,00", erro.Message);
    }

    [Fact]
    public async Task CalcularAsync_nao_altera_a_quantidade_disponivel()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto, quantidade: 5);
        var servico = Servico(contexto);

        // Quem consome e ConsumirAsync, dentro da transacao do pedido. Se o
        // calculo consumisse, a previa do cupom no checkout gastaria usos.
        await servico.CalcularAsync("NATAL20", 100m, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(5, cupom.QuantidadeDisponivel);
    }

    // ----Consumo e devolucao ---------------------------------------------

    [Fact]
    public async Task DevolverAsync_devolve_uma_unidade()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto, quantidade: 3);
        var servico = Servico(contexto);

        await servico.DevolverAsync(cupom.Id, CancellationToken.None);

        // Cancelar um pedido nao pode custar um cupom ao cliente: a devolucao e o
        // que mantem o contador querendo dizer quantas vendas usaram a promocao.
        Assert.Equal(4, contexto.Cupons.Single().QuantidadeDisponivel);
    }

    [Fact]
    public async Task ConsumirAsync_recusa_quando_ja_esgotou()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto, quantidade: 0);
        var servico = Servico(contexto);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.ConsumirAsync(cupom.Id, CancellationToken.None));

        Assert.Equal("Cupom inválido.", erro.Message);
    }

    [Fact]
    public async Task ExcluirAsync_remove_o_cupom()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var cupom = await SemearAsync(contexto);
        var servico = Servico(contexto);

        await servico.ExcluirAsync(cupom.Id, CancellationToken.None);

        Assert.Empty(contexto.Cupons.ToList());
    }

    [Fact]
    public async Task ExcluirAsync_recusa_id_inexistente()
    {
        using var contexto = Testes.CriarContextoInMemory();
        var servico = Servico(contexto);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            servico.ExcluirAsync(999, CancellationToken.None));
    }

    [Theory]
    [InlineData("natal20", "NATAL20")]
    [InlineData("  natal20  ", "NATAL20")]
    [InlineData("NATAL-20", "NATAL-20")]
    [InlineData("Natal 20!", "NATAL20")]
    public void NormalizarCodigo_deixa_o_texto_como_o_cliente_digita(string entrada, string esperado)
    {
        Assert.Equal(esperado, GestaoDeCuponsService.NormalizarCodigo(entrada));
    }

    private static RequisicaoDeCupom Requisicao(
        string codigo,
        decimal percentual,
        int quantidade,
        decimal valorMinimo,
        DateTime? validade,
        bool ativo) => new(codigo, percentual, quantidade, valorMinimo, validade, ativo);
}
