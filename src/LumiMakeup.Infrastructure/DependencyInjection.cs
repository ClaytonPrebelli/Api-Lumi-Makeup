using System.Security.Authentication;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Entities;
using LumiMakeup.Infrastructure.Integrations;
using LumiMakeup.Infrastructure.Persistence;
using LumiMakeup.Infrastructure.Security;
using LumiMakeup.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LumiMakeup.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        => services.AddInfrastructure(configuration, ServerVersion.AutoDetect);

    internal static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<string, ServerVersion> resolverDeVersaoDoServidor)
    {
        var connectionString = configuration.GetConnectionString("ConexaoPadrao")
            ?? throw new InvalidOperationException("Connection string 'ConexaoPadrao' não configurada.");

        services.AddDbContext<LumiDbContext>(options =>
            options.UseMySql(
                connectionString,
                resolverDeVersaoDoServidor(connectionString)));

        services.AddHttpClient<IViaCepService, ViaCepService>(client =>
        {
            client.BaseAddress = new Uri("https://viacep.com.br/");
        });

        // A cadeia de provedores. O primeiro é o Photon porque devolve o
        // logradouro como via quando há rua, e o CEP exato quando não há - sem
        // chave, que era o requisito. O ArcGIS fica atrás dele.
        //
        // O BaseAddress é do Photon porque é o primeiro da cadeia, e a consulta
        // dele usa caminho relativo. Sem ele, o HttpClient recusa a requisição
        // com "an invalid request URI was provided" antes de sair para a rede -
        // o que faria os dois provedores falharem por um motivo que não tem
        // nada a ver com a rede.
        services.AddHttpClient<IGeocodificador, GeocodificadorEmCadeia>(client =>
        {
            client.BaseAddress = new Uri("https://photon.komoot.io/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        services.AddHttpClient<INominatimService, NominatimService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("lumi-makeup/1.0 (contato@lumimakeup.com.br)");
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            // O Windows Server nao consegue fechar o TLS com o Nominatim: o proxy
            // responde "HandshakeFailure". A saida HTTPS do servidor funciona -
            // ViaCEP, Google e o proprio site respondem 200 - entao o problema nao
            // e' saida de rede nem certificado sem confianca, e' a negociacao.
            //
            // TLS 1.2 e' pedido explicitamente porque o padrao do .NET no Windows
            // antigo ainda oferece 1.0 e 1.1, e o proxy recusa o pacote inteiro
            // quando um deles vem junto.
            var handler = new HttpClientHandler();

            if (OperatingSystem.IsWindows())
            {
                handler.SslProtocols = SslProtocols.Tls12;
            }

            return handler;
        });

        services.AddScoped<IEnviadorDeEmailSmtp, EnviadorDeEmailSmtpViaClienteSmtp>();
        services.Configure<SmtpOptions>(configuration.GetSection("ExternalServices:Smtp"));

        var smtpConfigurado = !string.IsNullOrWhiteSpace(configuration["ExternalServices:Smtp:Host"]);
        if (smtpConfigurado)
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, EmailSenderStub>();
        }

        services.AddHttpClient<IRecaptchaValidator, RecaptchaValidator>(client =>
        {
            client.BaseAddress = new Uri("https://www.google.com/");
        });
        services.Configure<RecaptchaOptions>(configuration.GetSection("ExternalServices:Recaptcha"));

        services.AddHttpClient<IMelhoradorDeTextoService, MelhoradorDeTextoOpenAiCompativel>(client =>
        {
            var ia = configuration.GetSection("ExternalServices:Ia");
            var timeout = int.TryParse(ia["TimeoutEmSegundos"], out var segundos) ? segundos : 45;
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });
        services.Configure<OpcoesDeIa>(configuration.GetSection("ExternalServices:Ia"));
        services.Configure<ArmazenamentoDeImagensOptions>(configuration.GetSection("ArmazenamentoDeImagens"));
        services.AddScoped<IArmazenamentoDeImagens, ArmazenamentoDeImagensLocal>();

        RegistrarWhatsApp(services, configuration);

        services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();

        services.Configure<FrontendOptions>(configuration.GetSection("Frontend"));
        services.AddScoped<IRecuperacaoDeSenhaService, RecuperacaoDeSenhaService>();

        services.AddScoped<ICatalogoService, CatalogoService>();
        services.AddScoped<IGestaoDeProdutosService, GestaoDeProdutosService>();
        services.AddScoped<IGestaoDeCategoriasService, GestaoDeCategoriasService>();
        services.AddScoped<IGestaoDeBannersService, GestaoDeBannersService>();
        services.AddScoped<IGestaoDeCuponsService, GestaoDeCuponsService>();
        services.AddScoped<IGestaoDeClientesService, GestaoDeClientesService>();
        services.AddScoped<IGestaoDeEnderecosService, GestaoDeEnderecosService>();
        services.AddScoped<ICalculoDeFreteService, CalculoDeFreteService>();
        services.AddScoped<IGestaoDeFreteService, GestaoDeFreteService>();
        services.AddScoped<IGestaoDePedidosService, GestaoDePedidosService>();
        services.AddScoped<INotificadorDePedido, NotificadorDePedido>();
        services.AddScoped<RepositorioDeSessaoWhatsApp>();

        services.Configure<NotificacoesDePedidoOptions>(
            configuration.GetSection("NotificacoesDePedido"));

        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.Configure<TokenJwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        services.Configure<AutenticacaoGoogleOptions>(configuration.GetSection("ExternalServices:Google"));
        services.AddScoped<IAutenticacaoGoogleService, AutenticacaoGoogleService>();

        services.AddScoped<IAutenticacaoService, AutenticacaoService>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>
    /// Escolhe entre o cliente HTTP do Baileys e o stub.
    ///
    /// O stub continua sendo o padrao porque o WhatsApp e acessorio: sem
    /// segredo, sem pasta do Node ou com <c>Habilitado</c> desligado, a loja
    /// precisa vender do mesmo jeito, so sem o aviso. Subir o Node e opcional
    /// (<c>IniciarProcesso</c>), porque em desenvolvimento ele roda na mao, com
    /// <c>npm start</c>.
    /// </summary>
    private static void RegistrarWhatsApp(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpcoesDeBaileys>(configuration.GetSection("ExternalServices:Baileys"));

        // Singleton: o supervisor escreve o motivo da falha e a tela le. Sao
        // duas pontas do mesmo processo, e o estado nao pode depender do
        // escopo do request.
        services.AddSingleton<EstadoDoNodeBaileys>();

        var secao = configuration.GetSection("ExternalServices:Baileys");

        var habilitado = secao.GetValue("Habilitado", defaultValue: false);
        var segredo = secao["SegredoCompartilhado"] ?? string.Empty;

        if (!habilitado || string.IsNullOrWhiteSpace(segredo))
        {
            services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();

            return;
        }

        var porta = secao.GetValue("Porta", defaultValue: 3001);
        var urlBase = secao["UrlBase"];
        var timeout = secao.GetValue("TimeoutDoEnvioEmSegundos", defaultValue: 15);

        // A porta vem do mesmo lugar que a URL: o Node escuta em uma e a API
        // fala na outra, e divergir entre as duas so produz "nao conecta".
        var baseAddress = string.IsNullOrWhiteSpace(urlBase)
            ? $"http://127.0.0.1:{porta}"
            : urlBase.TrimEnd('/') + "/";

        services.AddHttpClient<IWhatsAppService, BaileysWhatsAppService>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(timeout);
        });

        if (secao.GetValue("IniciarProcesso", defaultValue: false))
        {
            services.AddHostedService<SupervisorDeNodeBaileys>();
        }
    }
}
