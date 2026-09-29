using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;

namespace LumiMakeup.Tests.Helpers;

internal static class Testes
{
    public static LumiDbContext CriarContextoInMemory(string? nomeDoBanco = null)
    {
        var opcoes = new DbContextOptionsBuilder<LumiDbContext>()
            .UseInMemoryDatabase(nomeDoBanco ?? Guid.NewGuid().ToString())
            // O provedor InMemory nao tem transacao: BeginTransaction e ignorado, e
            // o padrao e lancar. GestaoDePedidosService abre transacao de verdade em
            // producao, e sem esta linha nenhum teste de pedido rodaria aqui.
            //
            // O que a suíte perde com isso: nenhum teste comprova o rollback. Ela
            // comprova a ordem das escritas e os valores gravados, que e o que dava
            // para verificar sem banco relacional.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new LumiDbContext(opcoes);
    }

    public sealed class AmbienteSimulado : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "LumiMakeup.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new NullFileProvider();
    }

    public static Microsoft.Extensions.Hosting.IHostEnvironment CriarAmbiente(string contentRootPath)
    {
        return new AmbienteSimulado { ContentRootPath = contentRootPath };
    }

    public sealed class ManipuladorHttpSimulado : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _fabricaDeRespostas;

        public ManipuladorHttpSimulado(HttpResponseMessage resposta)
            : this(_ => resposta)
        {
        }

        public ManipuladorHttpSimulado(Func<HttpRequestMessage, HttpResponseMessage> fabricaDeRespostas)
        {
            _fabricaDeRespostas = fabricaDeRespostas;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_fabricaDeRespostas(request));
        }
    }

    public static HttpClient CriarHttpClient(HttpMessageHandler manipulador, string baseAddress = "https://localhost/")
    {
        return new HttpClient(manipulador)
        {
            BaseAddress = new Uri(baseAddress)
        };
    }
}