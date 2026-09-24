using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using LumiMakeup.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Porta { get; set; } = 465;
    public string Usuario { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string Remetente { get; set; } = string.Empty;
    public string NomeDoRemetente { get; set; } = string.Empty;
    public bool UsarSsl { get; set; } = true;
}

public interface IEnviadorDeEmailSmtp
{
    Task EnviarAsync(MailMessage mensagem, CancellationToken cancellationToken = default);
}

internal sealed class EnviadorDeEmailSmtpViaClienteSmtp : IEnviadorDeEmailSmtp
{
    private readonly SmtpOptions _opcoes;

    public EnviadorDeEmailSmtpViaClienteSmtp(IOptions<SmtpOptions> opcoes)
    {
        _opcoes = opcoes.Value;
    }

    [ExcludeFromCodeCoverage]
    public async Task EnviarAsync(MailMessage mensagem, CancellationToken cancellationToken = default)
    {
        using var cliente = new MailKit.Net.Smtp.SmtpClient { Timeout = 15_000 };

        var seguranca = (_opcoes.UsarSsl, _opcoes.Porta) switch
        {
            (true, < 587) => SecureSocketOptions.SslOnConnect,
            (true, _) => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.None
        };

        try
        {
            await cliente.ConnectAsync(_opcoes.Host, _opcoes.Porta, seguranca, cancellationToken);
            await cliente.AuthenticateAsync(_opcoes.Usuario, _opcoes.Senha, cancellationToken);
            await cliente.SendAsync(ConstruirMime(mensagem), cancellationToken);
            await cliente.DisconnectAsync(quit: true, cancellationToken);
        }
        catch
        {
            cliente.Disconnect(quit: false);
            throw;
        }
    }

    private MimeMessage ConstruirMime(MailMessage mensagem)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(mensagem.From.DisplayName, mensagem.From.Address));
        foreach (var destino in mensagem.To)
        {
            mime.To.Add(new MailboxAddress(destino.DisplayName, destino.Address));
        }

        mime.Subject = mensagem.Subject;
        mime.Body = new TextPart("html") { Text = mensagem.Body };
        return mime;
    }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpOptions> _opcoes;
    private readonly IEnviadorDeEmailSmtp _enviador;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> opcoes, IEnviadorDeEmailSmtp enviador, ILogger<SmtpEmailSender> logger)
    {
        _opcoes = opcoes;
        _enviador = enviador;
        _logger = logger;
    }

    public async Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default)
    {
        var mensagem = ConstruirMensagem(destino, assunto, corpoHtml);

        try
        {
            await _enviador.EnviarAsync(mensagem, cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao enviar e-mail via SMTP. Para={Destino} Assunto={Assunto}", destino, assunto);
        }
    }

    private MailMessage ConstruirMensagem(string destino, string assunto, string corpoHtml)
    {
        var remetente = new MailAddress(_opcoes.Value.Remetente, _opcoes.Value.NomeDoRemetente);

        var mensagem = new MailMessage
        {
            From = remetente,
            Subject = assunto,
            Body = corpoHtml,
            IsBodyHtml = true
        };

        mensagem.To.Add(destino);
        return mensagem;
    }
}