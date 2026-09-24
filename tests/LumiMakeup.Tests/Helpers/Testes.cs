using LumiMakeup.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LumiMakeup.Tests.Helpers;

internal static class Testes
{
    public static LumiDbContext CriarContextoInMemory(string? nomeDoBanco = null)
    {
        var opcoes = new DbContextOptionsBuilder<LumiDbContext>()
            .UseInMemoryDatabase(nomeDoBanco ?? Guid.NewGuid().ToString())
            .Options;

        return new LumiDbContext(opcoes);
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