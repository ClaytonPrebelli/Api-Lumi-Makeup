using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Security;
using LumiMakeup.Infrastructure.Services;
using LumiMakeup.Tests.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure;

public class DependencyInjectionTests
{
    private static IConfiguration CriarConfiguracao(bool comConexao = true)
    {
        var valores = new Dictionary<string, string?> { };
        if (comConexao)
        {
            valores.Add("ConnectionStrings:ConexaoPadrao", "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;");
        }
        valores.Add("Jwt:Segredo", "chave-secreta-super-segura-com-mais-de-32-bytes-0123456789");
        valores.Add("Jwt:Emissor", "lumi-makeup");
        valores.Add("Jwt:Audiencia", "lumi-makeup-testes");
        valores.Add("ExternalServices:Recaptcha:ChaveSecreta", "recaptcha");
        valores.Add("ExternalServices:Recaptcha:LimiteDeScore", "0.7");
        valores.Add("ExternalServices:Google:IdCliente", "cliente-google");
        valores.Add("ArmazenamentoDeImagens:CaminhoBase", Path.Combine(AppContext.BaseDirectory, "imagens-de-teste"));
        valores.Add("ArmazenamentoDeImagens:PastaPadrao", "produtos");
        valores.Add("ArmazenamentoDeImagens:TamanhoMaximoEmBytes", "5242880");

        return new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
    }

    [Fact]
    public void AddInfrastructure_lanca_quando_connection_string_nao_configurada()
    {
        var services = new ServiceCollection();

        var excecao = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(CriarConfiguracao(comConexao: false)));

        Assert.Equal("Connection string 'ConexaoPadrao' não configurada.", excecao.Message);
    }

    [Fact]
    public void AddInfrastructure_registra_servicos_e_opcoes()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        services.AddInfrastructure(
            CriarConfiguracao(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        Assert.NotNull(provedor.GetRequiredService<LumiDbContext>());
        Assert.NotNull(provedor.GetRequiredService<IViaCepService>());
        Assert.NotNull(provedor.GetRequiredService<IGeocodificador>());
        Assert.NotNull(provedor.GetRequiredService<IRecaptchaValidator>());
        Assert.NotNull(provedor.GetRequiredService<IEmailSender>());
        Assert.NotNull(provedor.GetRequiredService<IArmazenamentoDeImagens>());
        Assert.NotNull(provedor.GetRequiredService<IWhatsAppService>());
        Assert.NotNull(provedor.GetRequiredService<IFocusNfeService>());
        Assert.NotNull(provedor.GetRequiredService<ICatalogoService>());
        Assert.NotNull(provedor.GetRequiredService<IGestaoDeProdutosService>());
        Assert.NotNull(provedor.GetRequiredService<IGestaoDeCategoriasService>());
        Assert.NotNull(provedor.GetRequiredService<IPasswordHasher<Usuario>>());
        Assert.NotNull(provedor.GetRequiredService<ITokenService>());
        Assert.NotNull(provedor.GetRequiredService<IAutenticacaoGoogleService>());
        Assert.NotNull(provedor.GetRequiredService<IAutenticacaoService>());
        Assert.NotNull(provedor.GetRequiredService<IRecuperacaoDeSenhaService>());
        Assert.NotNull(provedor.GetRequiredService<DatabaseSeeder>());
        Assert.IsType<EmailSenderStub>(provedor.GetRequiredService<IEmailSender>());
        Assert.NotNull(provedor.GetRequiredService<IOptions<RecaptchaOptions>>().Value);
        Assert.NotNull(provedor.GetRequiredService<IOptions<TokenJwtOptions>>().Value);
        Assert.NotNull(provedor.GetRequiredService<IOptions<AutenticacaoGoogleOptions>>().Value);
        Assert.NotNull(provedor.GetRequiredService<IOptions<SmtpOptions>>().Value);
        Assert.NotNull(provedor.GetRequiredService<IOptions<FrontendOptions>>().Value);
        Assert.NotNull(provedor.GetRequiredService<IOptions<ArmazenamentoDeImagensOptions>>().Value);
    }

    [Fact]
    public void AddInfrastructure_com_smtp_configurado_registra_o_smtp_email_sender()
    {
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;",
            ["ExternalServices:Smtp:Host"] = "mail.lumimakeup.com.br",
            ["ExternalServices:Smtp:Porta"] = "465",
            ["ExternalServices:Smtp:Usuario"] = "nao-responda@lumimakeup.com.br",
            ["ExternalServices:Smtp:Senha"] = "segredo",
            ["ExternalServices:Smtp:Remetente"] = "nao-responda@lumimakeup.com.br",
            ["ExternalServices:Smtp:NomeDoRemetente"] = "Lumi Makeup"
        };
        var configuracao = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuracao, _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        Assert.IsType<SmtpEmailSender>(provedor.GetRequiredService<IEmailSender>());
    }

    [Fact]
    public void AddInfrastructure_publico_registra_os_descriptores()
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(CriarConfiguracao());

        var tiposEsperados = new[]
        {
            typeof(LumiDbContext),
            typeof(IViaCepService),
            typeof(IGeocodificador),
            typeof(IRecaptchaValidator),
            typeof(IEmailSender),
            typeof(IArmazenamentoDeImagens),
            typeof(IWhatsAppService),
            typeof(IFocusNfeService),
            typeof(ICatalogoService),
            typeof(IGestaoDeProdutosService),
            typeof(IGestaoDeCategoriasService),
            typeof(IPasswordHasher<Usuario>),
            typeof(ITokenService),
            typeof(IAutenticacaoGoogleService),
            typeof(IAutenticacaoService),
            typeof(IEnviadorDeEmailSmtp),
            typeof(DatabaseSeeder)
        };

        foreach (var tipo in tiposEsperados)
        {
            Assert.Contains(services, d => d.ServiceType == tipo);
        }
    }

    [Fact]
    public void AddInfrastructure_registra_o_stub_de_whatsapp_quando_o_baileys_nao_esta_habilitado()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        services.AddInfrastructure(
            CriarConfiguracao(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        // Padrao: sem Habilitado, o stub. O WhatsApp e acessorio e nao pode
        // derrubar a loja por estar fora do ar.
        Assert.IsType<WhatsAppServiceStub>(provedor.GetRequiredService<IWhatsAppService>());
    }

    [Fact]
    public void AddInfrastructure_registra_o_stub_quando_habilitado_mas_sem_segredo()
    {
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;",
            ["ExternalServices:Baileys:Habilitado"] = "true",
            ["ExternalServices:Baileys:SegredoCompartilhado"] = ""
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        services.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        // Habilitado sem segredo e o pior caso: a API subiria achando que envia,
        // e todo pedido perderia o aviso sem erro. O stub mantem a loja inteira.
        Assert.IsType<WhatsAppServiceStub>(provedor.GetRequiredService<IWhatsAppService>());
    }

    [Fact]
    public void AddInfrastructure_registra_o_cliente_http_quando_o_baileys_esta_habilitado_com_segredo()
    {
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;",
            ["ExternalServices:Baileys:Habilitado"] = "true",
            ["ExternalServices:Baileys:SegredoCompartilhado"] = "segredo-de-teste"
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        services.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        Assert.IsType<BaileysWhatsAppService>(provedor.GetRequiredService<IWhatsAppService>());

        var opcoes = provedor.GetRequiredService<IOptions<OpcoesDeBaileys>>().Value;

        Assert.True(opcoes.Habilitado);
        Assert.Equal("segredo-de-teste", opcoes.SegredoCompartilhado);
    }

    [Fact]
    public void AddInfrastructure_so_registra_o_supervisor_quando_o_node_debe_ser_subido_pela_api()
    {
        // Em desenvolvimento o Node roda na mao, com npm start. Sem este teste,
        // o supervisor subiria um segundo Node na mesma porta e um dos dois
        // ficaria com EADDRINUSE.
        var valores = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;",
            ["ExternalServices:Baileys:Habilitado"] = "true",
            ["ExternalServices:Baileys:SegredoCompartilhado"] = "segredo-de-teste"
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        services.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        Assert.DoesNotContain(
            services,
            d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                 && d.ImplementationType == typeof(SupervisorDeNodeBaileys));

        valores.Add("ExternalServices:Baileys:IniciarProcesso", "true");

        var comSupervisor = new ServiceCollection();
        comSupervisor.AddLogging();
        comSupervisor.AddSingleton(Testes.CriarAmbiente(AppContext.BaseDirectory));

        comSupervisor.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(valores).Build(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        Assert.Contains(
            comSupervisor,
            d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                 && d.ImplementationType == typeof(SupervisorDeNodeBaileys));
    }
}
