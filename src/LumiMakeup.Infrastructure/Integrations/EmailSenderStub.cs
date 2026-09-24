using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class EmailSenderStub : IEmailSender
{
    private readonly ILogger<EmailSenderStub> _logger;

    public EmailSenderStub(ILogger<EmailSenderStub> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "EmailSenderStub: e-mail NÃO enviado (integração Brevo pendente). Para={Destino} Assunto={Assunto}",
            destino,
            assunto);
        return Task.CompletedTask;
    }
}