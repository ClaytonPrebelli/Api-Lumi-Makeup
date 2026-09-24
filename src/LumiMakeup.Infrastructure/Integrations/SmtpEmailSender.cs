using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mail;
using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        using var cliente = new SmtpClient(_opcoes.Host, _opcoes.Porta)
        {
            EnableSsl = _opcoes.UsarSsl,
            Credentials = new NetworkCredential(_opcoes.Usuario, _opcoes.Senha),
            Timeout = 15_000
        };

        await cliente.SendMailAsync(mensagem, cancellationToken);
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