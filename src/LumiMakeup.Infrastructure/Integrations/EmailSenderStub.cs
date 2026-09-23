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

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "EmailSenderStub: e-mail NÃO enviado (integração Brevo pendente). To={To} Subject={Subject}",
            to,
            subject);
        return Task.CompletedTask;
    }
}