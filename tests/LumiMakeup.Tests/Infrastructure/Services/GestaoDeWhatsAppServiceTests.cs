using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class GestaoDeWhatsAppServiceTests
{
    private static GestaoDeWhatsAppService Criar()
    {
        return new GestaoDeWhatsAppService(
            Testes.CriarContextoInMemory(),
            Options.Create(new NotificacoesDePedidoOptions
            {
                NomeDaLoja = "Lumi Makeup",
                MensagemInicialWhatsAppCliente = "Oi, {Nome}! Aqui é da {Loja}."
            }));
    }

    [Fact]
    public async Task ObterAsync_sem_configuracao_devolve_a_frase_padrao()
    {
        // Loja que nunca salvou frase nenhuma ainda precisa de uma: sem isso a
        // mensagem sairia comecando em branco, e o cliente receberia a lista de
        // itens sem nenhuma apresentacao.
        var resultado = await Criar().ObterAsync();

        Assert.Equal(0, resultado.Id);
        Assert.Equal("Oi, {Nome}! Aqui é da {Loja}.", resultado.MensagemInicialCliente);
    }

    [Fact]
    public async Task SalvarAsync_grava_a_frase_e_o_ObterAsync_devolve_de_volta()
    {
        var servico = Criar();

        var salvo = await servico.SalvarAsync(
            new RequisicaoDeConfiguracaoWhatsApp("Oi, {Nome}! Seu pedido chegou!"));

        Assert.True(salvo.Id > 0);

        var lido = await servico.ObterAsync();

        Assert.Equal(salvo.MensagemInicialCliente, lido.MensagemInicialCliente);
    }

    [Fact]
    public async Task SalvarAsync_de_novo_sobrescreve_em_vez_de_criar_outra_linha()
    {
        // Duas linhas fariam o servico escolher uma delas por ordem de Id, e a
        // frase que a administradora achou que salvou seria a outra.
        var servico = Criar();

        await servico.SalvarAsync(new RequisicaoDeConfiguracaoWhatsApp("Primeira"));
        await servico.SalvarAsync(new RequisicaoDeConfiguracaoWhatsApp("Segunda"));

        var lido = await servico.ObterAsync();

        Assert.Equal("Segunda", lido.MensagemInicialCliente);
    }

    [Fact]
    public async Task SalvarAsync_recusa_frase_vazia()
    {
        var servico = Criar();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.SalvarAsync(new RequisicaoDeConfiguracaoWhatsApp("   ")));
    }

    [Fact]
    public async Task SalvarAsync_recusa_frase_maior_que_o_limite()
    {
        var servico = Criar();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servico.SalvarAsync(new RequisicaoDeConfiguracaoWhatsApp(new string('a', 501))));
    }

    [Fact]
    public async Task GerarPreviaAsync_mostra_itens_totais_e_o_pedido_de_confirmacao()
    {
        // A previa e o que a administradora usa para decidir a frase. Se faltar
        // item, total ou o pedido de resposta, ela decide sem ver o que o
        // cliente recebe.
        var previa = await Criar().GerarPreviaAsync(null);

        Assert.Contains("Batom Matte", previa.Mensagem);
        Assert.Contains("Pó Compacto", previa.Mensagem);
        Assert.Contains("Subtotal", previa.Mensagem);
        Assert.Contains("Frete", previa.Mensagem);
        Assert.Contains("Total", previa.Mensagem);
        Assert.Contains("R$ 91,80", previa.Mensagem);
        Assert.Contains("Endereço de entrega", previa.Mensagem);
        Assert.Contains("Confirme seu pedido", previa.Mensagem);
    }

    [Fact]
    public async Task GerarPreviaAsync_troca_os_marcadores_pelo_dado_de_exemplo()
    {
        var previa = await Criar().GerarPreviaAsync("Oi, {Nome}! Aqui é da {Loja}.");

        // O marcador que nao foi trocado apareceria com chave e virgula na
        // mensagem do cliente, e ninguem perceberia ate a venda estar tomada.
        Assert.Contains("Oi, Maria! Aqui é da Lumi Makeup.", previa.Mensagem);
        Assert.DoesNotContain("{Nome}", previa.Mensagem);
        Assert.DoesNotContain("{Loja}", previa.Mensagem);
    }

    [Fact]
    public async Task GerarPreviaAsync_usa_a_frase_que_a_tela_mandou_e_nao_a_gravada()
    {
        // A administradora precisa ver o efeito da frase nova antes de salvar.
        // Se a previa usasse a gravada, ela so veria o resultado depois de
        // gravar, que e tarde para decidir o texto.
        var servico = Criar();

        await servico.SalvarAsync(new RequisicaoDeConfiguracaoWhatsApp("Frase antiga"));

        var previa = await servico.GerarPreviaAsync("Frase nova que ainda nao foi salva");

        Assert.Contains("Frase nova que ainda nao foi salva", previa.Mensagem);
        Assert.DoesNotContain("Frase antiga", previa.Mensagem);
    }

    [Fact]
    public async Task GerarPreviaAsync_marca_os_titulos_com_asterisco_para_o_negrito()
    {
        // WhatsApp negrita com asterisco simples, e nao com ** como Markdown.
        // Marcador errado aqui significa titulo em negrito na tela de previa e
        // texto cru no aparelho do cliente.
        var previa = await Criar().GerarPreviaAsync("Oi!");

        Assert.Contains("*Itens do Pedido*", previa.Mensagem);
        Assert.Contains("*Total:*", previa.Mensagem);
        Assert.DoesNotContain("**", previa.Mensagem);
    }

    [Fact]
    public async Task GerarPreviaAsync_omite_o_bloco_de_endereco_quando_o_pedido_e_de_balcao()
    {
        // Pedido de balcao nao tem onde entregar. Inventar um endereco na previa
        // faria a administradora achar que a mensagem do cliente tambem mostra
        // um, e nao mostra.
        var semEndereco = MontadorDeMensagemDePedido.PedidoDeExemplo() with
        {
            EnderecoLogradouro = null,
            EnderecoNumero = null,
            EnderecoComplemento = null,
            EnderecoBairro = null,
            EnderecoCidade = null,
            EnderecoEstado = null,
            EnderecoCep = null
        };

        var mensagem = MontadorDeMensagemDePedido.Montar("Oi!", semEndereco, "Lumi Makeup");

        Assert.DoesNotContain("Endereço de entrega", mensagem);
        Assert.Contains("Confirme seu pedido", mensagem);
    }
}