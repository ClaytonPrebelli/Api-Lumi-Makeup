using LumiMakeup.Aplicacao.Abstracoes;
using LumiMakeup.Dominio.Entidades;
using LumiMakeup.Infraestrutura.Integracoes;
using LumiMakeup.Infraestrutura.Persistencia;
using LumiMakeup.Infraestrutura.Seguranca;
using LumiMakeup.Infraestrutura.Servicos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LumiMakeup.Infraestrutura;

public static class InjecaoDeDependencia
{
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ConexaoPadrao")
            ?? throw new InvalidOperationException("Connection string 'ConexaoPadrao' não configurada.");

        services.AddDbContext<ContextoLumi>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)));

        services.AddHttpClient<IServicoViaCep, ServicoViaCep>(client =>
        {
            client.BaseAddress = new Uri("https://viacep.com.br/");
        });

        services.AddHttpClient<IServicoNominatim, ServicoNominatim>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("lumi-makeup/1.0 (contato@lumimakeup.com.br)");
        });

        services.AddScoped<IRemetenteDeEmail, RemetenteDeEmailStub>();
        services.AddHttpClient<IValidadorDeRecaptcha, ValidadorDeRecaptcha>(client =>
        {
            client.BaseAddress = new Uri("https://www.google.com/");
        });
        services.Configure<OpcoesDeRecaptcha>(configuration.GetSection("ServicosExternos:Recaptcha"));
        services.AddScoped<IServicoCloudinary, ServicoCloudinaryStub>();
        services.AddScoped<IServicoDeWhatsApp, ServicoDeWhatsAppStub>();
        services.AddScoped<IServicoFocusNfe, ServicoFocusNfeStub>();

        services.AddScoped<IServicoDeCatalogo, ServicoDeCatalogo>();

        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.Configure<OpcoesDeTokenJwt>(configuration.GetSection("Jwt"));
        services.AddScoped<IServicoDeToken, ServicoDeTokenJwt>();

        services.Configure<OpcoesDeAutenticacaoGoogle>(configuration.GetSection("ServicosExternos:Google"));
        services.AddScoped<IServicoDeAutenticacaoGoogle, ServicoDeAutenticacaoGoogle>();

        services.AddScoped<IServicoDeAutenticacao, ServicoDeAutenticacao>();

        services.AddScoped<GeradorDeDadosIniciais>();

        return services;
    }
}