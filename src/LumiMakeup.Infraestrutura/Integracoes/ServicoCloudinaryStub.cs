using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class ServicoCloudinaryStub : IServicoCloudinary
{
    private readonly ILogger<ServicoCloudinaryStub> _logger;

    public ServicoCloudinaryStub(ILogger<ServicoCloudinaryStub> logger)
    {
        _logger = logger;
    }

    public Task<string> EnviarAsync(Stream arquivo, string nomeDoArquivo, string pasta, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ServicoCloudinaryStub: upload NÃO realizado (integração Cloudinary pendente).");
        return Task.FromResult($"https://placeholder.cloudinary.com/{pasta}/{nomeDoArquivo}");
    }

    public Task ExcluirAsync(string idPublico, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ServicoCloudinaryStub: exclusão de imagem não realizada.");
        return Task.CompletedTask;
    }
}