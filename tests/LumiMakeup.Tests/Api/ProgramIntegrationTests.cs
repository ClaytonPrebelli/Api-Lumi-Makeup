using System.Net;
using System.Text.Json;
using LumiMakeup.Domain.Enums;
using LumiMakeup.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LumiMakeup.Tests.Api;

public class ProgramIntegrationTests
{
    private sealed class FabricaDeTeste : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _configuracao;
        private readonly string _nomeDoBanco;
        private readonly string _ambiente;

        public FabricaDeTeste(Dictionary<string, string?> configuracao, string ambiente = "Development")
        {
            _configuracao = configuracao;
            _nomeDoBanco = "lumi-inmemory-" + Guid.NewGuid().ToString("N");
            _ambiente = ambiente;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_ambiente);
            builder.UseContentRoot(ObterDiretorioDaApi());

            builder.ConfigureAppConfiguration((_, configuracao) =>
            {
                configuracao.AddInMemoryCollection(_configuracao);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LumiDbContext>>();
                services.AddDbContext<LumiDbContext>(opcoes => opcoes.UseInMemoryDatabase(_nomeDoBanco));
            });
        }

        public static Dictionary<string, string?> CriarConfiguracao(bool comSeed = true)
        {
            var valores = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;AllowPublicKeyRetrieval=True;",
                ["Jwt:Segredo"] = "chave-secreta-super-segura-com-mais-de-32-bytes-0123456789",
                ["Jwt:Emissor"] = "lumi-makeup",
                ["Jwt:Audiencia"] = "lumi-makeup-testes",
                ["Cors:OrigensPermitidas"] = "http://localhost:4200,http://localhost:5173"
            };

            if (comSeed)
            {
                valores["Autenticacao:SeedAdministrador:Email"] = "admin@lumimakeup.com.br";
                valores["Autenticacao:SeedAdministrador:Senha"] = "Admin@2026!";
            }
            else
            {
                valores["Autenticacao:SeedAdministrador:Email"] = string.Empty;
                valores["Autenticacao:SeedAdministrador:Senha"] = string.Empty;
            }

            return valores;
        }
    }

    public static string ObterDiretorioDaApi()
    {
        var diretorioAtual = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorioAtual is not null)
        {
            if (File.Exists(Path.Combine(diretorioAtual.FullName, "LumiMakeup.slnx")))
            {
                var diretorioDaApi = Path.Combine(diretorioAtual.FullName, "src", "LumiMakeup.Api");
                return Directory.Exists(diretorioDaApi) ? diretorioDaApi : AppContext.BaseDirectory;
            }

            diretorioAtual = diretorioAtual.Parent;
        }

        return AppContext.BaseDirectory;
    }

    [Fact]
    public async Task Saude_retorna_ok()
    {
        using var fabrica = new FabricaDeTeste(FabricaDeTeste.CriarConfiguracao());
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var conteudo = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync());
        Assert.Equal("ok", conteudo.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Swagger_gera_documentacao_em_ambiente_de_desenvolvimento()
    {
        using var fabrica = new FabricaDeTeste(FabricaDeTeste.CriarConfiguracao());
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using var conteudo = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync());
        Assert.Equal("3.0.1", conteudo.RootElement.GetProperty("openapi").GetString());
        Assert.True(conteudo.RootElement.GetProperty("paths").TryGetProperty("/api/autenticacao/cadastrar", out _));
    }

    [Fact]
    public async Task Em_ambiente_de_producao_nao_exige_cors_ou_swagger()
    {
        var configuracoes = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ConexaoPadrao"] = "Server=localhost;Database=lumimake_testes;User=root;Password=pwd;",
            ["Jwt:Segredo"] = "chave-secreta-super-segura-com-mais-de-32-bytes-0123456789",
            ["Jwt:Emissor"] = "lumi-makeup",
            ["Jwt:Audiencia"] = "lumi-makeup-testes",
            ["Autenticacao:SeedAdministrador:Email"] = string.Empty,
            ["Autenticacao:SeedAdministrador:Senha"] = string.Empty
        };

        using var fabrica = new FabricaDeTeste(configuracoes, ambiente: "Production");
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Com_seed_configurado_cria_administrador()
    {
        using var fabrica = new FabricaDeTeste(FabricaDeTeste.CriarConfiguracao(comSeed: true));
        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<LumiDbContext>();

        var administrador = await contexto.Usuarios.SingleAsync(u => u.Papel == PapelUsuario.Administrador);

        Assert.Equal("admin@lumimakeup.com.br", administrador.Email);
        Assert.NotNull(administrador.HashSenha);
    }

    [Fact]
    public async Task Sem_seed_configurado_nao_cria_administrador()
    {
        using var fabrica = new FabricaDeTeste(FabricaDeTeste.CriarConfiguracao(comSeed: false));
        using var escopo = fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<LumiDbContext>();

        var quantidadeDeAdministradores = await contexto.Usuarios.CountAsync(u => u.Papel == PapelUsuario.Administrador);

        Assert.Equal(0, quantidadeDeAdministradores);
    }
}