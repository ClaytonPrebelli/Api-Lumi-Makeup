using System.Net.Mail;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class SmtpEmailSenderTests
{
    private static SmtpOptions CriarOpcoes() => new()
    {
        Host = "mail.lumimakeup.com.br",
        Porta = 465,
        Usuario = "nao-responda@lumimakeup.com.br",
        Senha = "segredo",
        Remetente = "nao-responda@lumimakeup.com.br",
        NomeDoRemetente = "Lumi Makeup",
        UsarSsl = true
    };

    private sealed class EnviadorSimulado : IEnviadorDeEmailSmtp
    {
        public MailMessage? MensagemRecebida;
        public bool DeveFalhar;

        public Task EnviarAsync(MailMessage mensagem, CancellationToken cancellationToken = default)
        {
            if (DeveFalhar)
            {
                throw new SmtpException("Falha simulada.");
            }

            MensagemRecebida = mensagem;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task EnviarAsync_monta_mensagem_com_remetente_destino_e_html()
    {
        var enviador = new EnviadorSimulado();
        var remetente = new SmtpEmailSender(
            Options.Create(CriarOpcoes()),
            enviador,
            NullLogger<SmtpEmailSender>.Instance);

        await remetente.EnviarAsync("destino@exemplo.com", "Assunto teste", "<b>corpo</b>", CancellationToken.None);

        Assert.NotNull(enviador.MensagemRecebida);
        var mensagem = enviador.MensagemRecebida!;
        Assert.Equal("nao-responda@lumimakeup.com.br", mensagem.From!.Address);
        Assert.Equal("Lumi Makeup", mensagem.From.DisplayName);
        var destino = Assert.Single(mensagem.To);
        Assert.Equal("destino@exemplo.com", destino.Address);
        Assert.Equal("Assunto teste", mensagem.Subject);
        Assert.Equal("<b>corpo</b>", mensagem.Body);
        Assert.True(mensagem.IsBodyHtml);
    }

    [Fact]
    public async Task EnviarAsync_engole_falha_de_envio_sem_lancar()
    {
        var enviador = new EnviadorSimulado { DeveFalhar = true };
        var remetente = new SmtpEmailSender(
            Options.Create(CriarOpcoes()),
            enviador,
            NullLogger<SmtpEmailSender>.Instance);

        await remetente.EnviarAsync("destino@exemplo.com", "Assunto teste", "<b>corpo</b>", CancellationToken.None);
    }
}