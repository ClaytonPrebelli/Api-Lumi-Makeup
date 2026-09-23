using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class CloudinaryServiceStub : ICloudinaryService
{
    private readonly ILogger<CloudinaryServiceStub> _logger;

    public CloudinaryServiceStub(ILogger<CloudinaryServiceStub> logger)
    {
        _logger = logger;
    }

    public Task<string> UploadAsync(Stream file, string fileName, string folder, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CloudinaryServiceStub: upload NÃO realizado (integração Cloudinary pendente).");
        return Task.FromResult($"https://placeholder.cloudinary.com/{folder}/{fileName}");
    }

    public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("CloudinaryServiceStub: exclusão de imagem não realizada.");
        return Task.CompletedTask;
    }
}