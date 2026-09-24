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

        services.AddInfrastructure(
            CriarConfiguracao(),
            _ => new MySqlServerVersion(new Version(8, 0, 11)));

        var provedor = services.BuildServiceProvider();

        Assert.NotNull(provedor.GetRequiredService<LumiDbContext>());
        Assert.NotNull(provedor.GetRequiredService<IViaCepService>());
        Assert.NotNull(provedor.GetRequiredService<INominatimService>());
        Assert.NotNull(provedor.GetRequiredService<IRecaptchaValidator>());
        Assert.NotNull(provedor.GetRequiredService<IEmailSender>());
        Assert.NotNull(provedor.GetRequiredService<ICloudinaryService>());
        Assert.NotNull(provedor.GetRequiredService<IWhatsAppService>());
        Assert.NotNull(provedor.GetRequiredService<IFocusNfeService>());
        Assert.NotNull(provedor.GetRequiredService<ICatalogoService>());
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
            typeof(INominatimService),
            typeof(IRecaptchaValidator),
            typeof(IEmailSender),
            typeof(ICloudinaryService),
            typeof(IWhatsAppService),
            typeof(IFocusNfeService),
            typeof(ICatalogoService),
            typeof(IPasswordHasher<Usuario>),
            typeof(ITokenService),
            typeof(IAutenticacaoGoogleService),
            typeof(IAutenticacaoService),
            typeof(IRecuperacaoDeSenhaService),
            typeof(IEnviadorDeEmailSmtp),
            typeof(DatabaseSeeder)
        };

        foreach (var tipo in tiposEsperados)
        {
            Assert.Contains(services, d => d.ServiceType == tipo);
        }
    }
}