using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class ServicoDeWhatsAppStub : IServicoDeWhatsApp
{
    private readonly ILogger<ServicoDeWhatsAppStub> _logger;

    public ServicoDeWhatsAppStub(ILogger<ServicoDeWhatsAppStub> logger)
    {
        _logger = logger;
    }

    public Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "ServicoDeWhatsAppStub: mensagem NÃO enviada (microserviço Baileys pendente). Para={Telefone}",
            telefone);
        return Task.FromResult(false);
    }
}