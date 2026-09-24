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

        services.AddHttpClient<INominatimService, NominatimService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("lumi-makeup/1.0 (contato@lumimakeup.com.br)");
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
        services.AddScoped<ICloudinaryService, CloudinaryServiceStub>();
        services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();
        services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();

        services.Configure<FrontendOptions>(configuration.GetSection("Frontend"));
        services.AddScoped<IRecuperacaoDeSenhaService, RecuperacaoDeSenhaService>();

        services.AddScoped<ICatalogoService, CatalogoService>();

        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.Configure<TokenJwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        services.Configure<AutenticacaoGoogleOptions>(configuration.GetSection("ExternalServices:Google"));
        services.AddScoped<IAutenticacaoGoogleService, AutenticacaoGoogleService>();

        services.AddScoped<IAutenticacaoService, AutenticacaoService>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}