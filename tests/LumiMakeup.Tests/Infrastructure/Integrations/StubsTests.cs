using System.Text;
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
    public async Task WhatsAppServiceStub_retorna_false_indicando_nao_enviado()
    {
        var stub = new WhatsAppServiceStub(NullLogger<WhatsAppServiceStub>.Instance);

        var enviado = await stub.EnviarMensagemAsync("5511999999999", "Olá", CancellationToken.None);

        Assert.False(enviado);
    }

    [Fact]
    public async Task CloudinaryServiceStub_envia_e_cria_url_do_placeholder()
    {
        var stub = new CloudinaryServiceStub(NullLogger<CloudinaryServiceStub>.Instance);
        using var arquivo = new MemoryStream(Encoding.UTF8.GetBytes("conteudo"));

        var url = await stub.EnviarAsync(arquivo, "foto.jpg", "produtos", CancellationToken.None);

        Assert.Equal("https://placeholder.cloudinary.com/produtos/foto.jpg", url);
    }

    [Fact]
    public async Task CloudinaryServiceStub_exclui_sem_erro()
    {
        var stub = new CloudinaryServiceStub(NullLogger<CloudinaryServiceStub>.Instance);

        await stub.ExcluirAsync("id-publico", CancellationToken.None);
    }

    [Fact]
    public async Task FocusNfeServiceStub_retorna_identificador_vazio()
    {
        var stub = new FocusNfeServiceStub(NullLogger<FocusNfeServiceStub>.Instance);

        var id = await stub.EmitirNotaAsync(123, CancellationToken.None);

        Assert.Equal(string.Empty, id);
    }
}