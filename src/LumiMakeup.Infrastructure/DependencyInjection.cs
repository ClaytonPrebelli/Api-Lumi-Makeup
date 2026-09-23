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
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

        services.AddDbContext<LumiDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)));

        services.AddHttpClient<IViaCepService, ViaCepService>(client =>
        {
            client.BaseAddress = new Uri("https://viacep.com.br/");
        });

        services.AddHttpClient<INominatimService, NominatimService>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("lumi-makeup/1.0 (contato@lumimakeup.com.br)");
        });

        services.AddScoped<IEmailSender, EmailSenderStub>();
        services.AddHttpClient<IRecaptchaValidator, RecaptchaValidator>(client =>
        {
            client.BaseAddress = new Uri("https://www.google.com/");
        });
        services.Configure<RecaptchaOptions>(configuration.GetSection("ExternalServices:Recaptcha"));
        services.AddScoped<ICloudinaryService, CloudinaryServiceStub>();
        services.AddScoped<IWhatsAppService, WhatsAppServiceStub>();
        services.AddScoped<IFocusNfeService, FocusNfeServiceStub>();

        services.AddScoped<ICatalogService, CatalogService>();

        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped<ITokenService, JwtTokenService>();

        services.Configure<GoogleAuthOptions>(configuration.GetSection("ExternalServices:Google"));
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}