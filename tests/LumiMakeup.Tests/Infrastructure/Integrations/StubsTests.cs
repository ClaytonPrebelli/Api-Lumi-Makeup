using LumiMakeup.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace LumiMakeup.Tests.Infrastructure.Integrations;

public class StubsTests
{
    [Fact]
    public async Task EmailSenderStub_envia_sem_erro()
    {
        var stub = new EmailSenderStub(NullLogger<EmailSenderStub>.Instance);

        await stub.EnviarAsync("destino@exemplo.com", "Assunto", "<p>corpo</p>", CancellationToken.None);
    }

    [Fact]
    public async Task EmailSenderStub_nao_confirma_envio_real()
    {
        var stub = new EmailSenderStub(NullLogger<EmailSenderStub>.Instance);

        var confirmado = await stub.EnviarComConfirmacaoAsync(
            "destino@exemplo.com", "Assunto", "<p>corpo</p>", CancellationToken.None);

        Assert.False(confirmado);
    }

    [Fact]
    public async Task WhatsAppServiceStub_retorna_false_indicando_nao_enviado()
    {
        var stub = new WhatsAppServiceStub(NullLogger<WhatsAppServiceStub>.Instance);

        var enviado = await stub.EnviarMensagemAsync("5511999999999", "Olá", CancellationToken.None);

        Assert.False(enviado);
    }

    [Fact]
    public async Task FocusNfeServiceStub_retorna_identificador_vazio()
    {
        var stub = new FocusNfeServiceStub(NullLogger<FocusNfeServiceStub>.Instance);

        var id = await stub.EmitirNotaAsync(123, CancellationToken.None);

        Assert.Equal(string.Empty, id);
    }
}