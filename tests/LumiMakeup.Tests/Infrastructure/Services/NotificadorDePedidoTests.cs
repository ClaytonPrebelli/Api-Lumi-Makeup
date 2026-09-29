using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LumiMakeup.Tests.Infrastructure.Services;

public sealed class NotificadorDePedidoTests
{
    private const string EmailDaAdmin = "use.lumimakeup@gmail.com";

    private static PedidoDto Pedido(
        string nome = "Ana Souza",
        string? telefone = "11999999999",
        string? email = "ana@exemplo.com",
        decimal desconto = 0m,
        string? cupom = null,
        StatusPedido status = StatusPedido.AguardandoPagamento) => new(
            1,
            7,
            nome,
            "123.456.789-00",
            telefone,
            email,
            OrigemPedido.Online,
            status,
            null,
            cupom,
            100m,
            desconto,
            15m,
            115m - desconto,
            null,
            new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            null,
            [new PedidoItemDto(1, "Batom matte", 1, 35m, null, 35m)]);

    private static NotificadorDePedido Criar(
        Mock<IEmailSender> email,
        Mock<IWhatsAppService> whatsApp,
        string? emailDaAdmin = EmailDaAdmin,
        bool avisarPorWhatsApp = true)
    {
        return new NotificadorDePedido(
            email.Object,
            whatsApp.Object,
            Options.Create(new NotificacoesDePedidoOptions
            {
                EmailDaAdministradora = emailDaAdmin ?? string.Empty,
                WhatsAppDaAdministradora = "5511888888888",
                NomeDaLoja = "Lumi Makeup",
                AvisarPorWhatsApp = avisarPorWhatsApp
            }),
            NullLogger<NotificadorDePedido>.Instance);
    }

    [Fact]
    public async Task PedidoCriadoAsync_avisa_o_cliente_e_a_administradora()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        var destinos = email.Invocations
            .Select(i => ((string)i.Arguments[0], (string)i.Arguments[1]))
            .ToList();

        Assert.Contains(destinos, d => d.Item1 == EmailDaAdmin);
        Assert.Contains(destinos, d => d.Item1 == "ana@exemplo.com");
    }

    [Fact]
    public async Task PedidoCriadoAsync_manda_o_resumo_com_o_total_para_a_administradora()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        var paraAdmin = email.Invocations.Single(i => (string)i.Arguments[0] == EmailDaAdmin);
        var corpo = (string)paraAdmin.Arguments[2];

        // A admin precisa ver telefone, total e o aviso de que a nota fiscal esta
        // pendente, senao ela nao sabe o que falta.
        Assert.Contains("11999999999", corpo);
        Assert.Contains("Batom matte", corpo);
        Assert.Contains("115,00", corpo);
        Assert.Contains("nota fiscal", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PedidoCriadoAsync_avisa_a_admin_mesmo_sem_contato_do_cliente()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(
            Pedido(telefone: null, email: null),
            CancellationToken.None);

        // E o e-mail da admin que dispara a acao comercial. Ele nao pode depender
        // de o cliente ter telefone e e-mail cadastrados.
        Assert.Contains(email.Invocations, i => (string)i.Arguments[0] == EmailDaAdmin);
    }

    [Fact]
    public async Task PedidoCriadoAsync_manda_whatsapp_para_o_cliente()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(w => w.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        var chamada = whatsApp.Invocations.Single();
        Assert.Equal("5511999999999", chamada.Arguments[0]);
        Assert.Contains("Batom matte", (string)chamada.Arguments[1]);
    }

    [Fact]
    public async Task PedidoCriadoAsync_ignora_whatsapp_quando_desligado_na_configuracao()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        // A sessao do Baileys ainda esta por vir, e o numero da admin muda com
        // frequencia. A chave desliga sem recompilar.
        await Criar(email, whatsApp, avisarPorWhatsApp: false)
            .PedidoCriadoAsync(Pedido(), CancellationToken.None);

        Assert.Empty(whatsApp.Invocations);
    }

    [Fact]
    public async Task PedidoCriadoAsync_so_avisa_a_admin_quando_o_email_dela_nao_estiver_configurado()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        // Sem o e-mail da admin, a venda sumiria sem nenhum sinal. O notificador
        // registra isso no log; aqui o que importa e que o cliente ainda avisa.
        await Criar(email, whatsApp, emailDaAdmin: null).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        Assert.DoesNotContain(email.Invocations, i => (string)i.Arguments[0] == EmailDaAdmin);
        Assert.Contains(email.Invocations, i => (string)i.Arguments[0] == "ana@exemplo.com");
    }

    [Fact]
    public async Task PedidoCriadoAsync_envia_o_email_da_admin_antes_do_cliente()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        // O e-mail da admin e o que inicia a acao comercial. Se o canal do cliente
        // falhar, o pedido ainda precisa chegar a quem pode fechar a venda.
        Assert.Equal(EmailDaAdmin, (string)email.Invocations[0].Arguments[0]);
    }

    [Fact]
    public async Task Falha_no_email_do_cliente_nao_impede_o_da_administradora()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();
        email.Setup(e => e.EnviarAsync("ana@exemplo.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP fora"));

        var notificador = Criar(email, whatsApp);

        // Nenhum aviso pode derrubar o pedido, e um canal quebrado não pode
        // silenciar o outro.
        await notificador.PedidoCriadoAsync(Pedido(), CancellationToken.None);

        Assert.Contains(email.Invocations, i => (string)i.Arguments[0] == EmailDaAdmin);
    }

    [Fact]
    public async Task Falha_no_whatsapp_nao_derruba_o_pedido()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();
        whatsApp.Setup(w => w.EnviarMensagemAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Baileys fora"));

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(), CancellationToken.None);

        Assert.Equal(2, email.Invocations.Count);
    }

    [Fact]
    public async Task Whatsapp_nao_envia_para_telefone_que_nao_e_numero()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(telefone: "123"), CancellationToken.None);

        // Melhor não enviar do que mandar para um número inválido e descobrir o
        // problema só no log do Baileys.
        Assert.Empty(whatsApp.Invocations);
    }

    [Fact]
    public async Task PedidoPagoAsync_avisa_o_cliente_com_a_confirmacao()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoPagoAsync(
            Pedido(status: StatusPedido.Pago),
            CancellationToken.None);

        Assert.Contains(email.Invocations, i => ((string)i.Arguments[1]).Contains("confirmado"));
    }

    [Fact]
    public async Task PedidoCanceladoAsync_avisa_o_cliente()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCanceladoAsync(
            Pedido(status: StatusPedido.Cancelado),
            CancellationToken.None);

        Assert.Contains(email.Invocations, i => ((string)i.Arguments[1]).Contains("cancelado"));
    }

    [Fact]
    public async Task O_nome_do_cliente_vai_escapado_no_html()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        // Nome vem de quem digitou no cadastro. Sem escapar, uma tag no nome
        // pareceria conteudo em vez de texto.
        await Criar(email, whatsApp).PedidoCriadoAsync(Pedido(nome: "<b>Ana</b>"), CancellationToken.None);

        var corpo = email.Invocations[0].Arguments[2] as string;
        Assert.Contains("&lt;b&gt;Ana", corpo);
        Assert.DoesNotContain("<b>Ana</b>", corpo);
    }

    [Fact]
    public async Task O_desconto_aparece_no_resumo_quando_hou_cupom()
    {
        var email = new Mock<IEmailSender>();
        var whatsApp = new Mock<IWhatsAppService>();

        await Criar(email, whatsApp).PedidoCriadoAsync(
            Pedido(desconto: 20m, cupom: "NATAL20"),
            CancellationToken.None);

        var corpo = email.Invocations[0].Arguments[2] as string;
        Assert.Contains("NATAL20", corpo);
        Assert.Contains("20,00", corpo);
    }

    [Theory]
    [InlineData("11999999999", "5511999999999")]
    [InlineData("(11) 99999-9999", "5511999999999")]
    [InlineData("+55 11 99999 9999", "5511999999999")]
    [InlineData("5511999999999", "5511999999999")]
    [InlineData("123", null)]
    [InlineData("", null)]
    [InlineData("abcdefghijk", null)]
    public void NormalizarTelefone_deixa_o_numero_no_formato_do_baileys(string entrada, string? esperado)
    {
        Assert.Equal(esperado, NotificadorDePedido.NormalizarTelefone(entrada));
    }
}
