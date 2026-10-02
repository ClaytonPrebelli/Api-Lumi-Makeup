using LumiMakeup.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infrastructure.Integrations;

public sealed class WhatsAppServiceStub : IWhatsAppService
{
    private readonly ILogger<WhatsAppServiceStub> _logger;

    public WhatsAppServiceStub(ILogger<WhatsAppServiceStub> logger)
    {
        _logger = logger;
    }

    public Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "WhatsAppServiceStub: mensagem simulada enviada. Para={Telefone}",
            telefone);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Diz "fora do ar" em vez de "pareado, mas nao conectado". O painel
    /// precisa conseguir distinguir os dois: sem o Node, o primeiro passo e
    /// instalar, e nao escanear QR.
    /// </summary>
    public Task<StatusDoWhatsApp> ObterStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new StatusDoWhatsApp(false, false, null, null, null, null, WhatsAppDesligado));

    /// <summary>
    /// Sem Node nao ha QR. A tela mostra que o servico esta desligado, em vez
    /// de ficar esperando um QR que nunca chega.
    /// </summary>
    public Task<string?> ObterQrDePareamentoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Sem Node nao ha conexao a forcar. Devolve null e a tela mostra que o
    /// servico esta desligado, em vez de simular que algo aconteceu.
    /// </summary>
    public Task<string?> ForcarReconexaoDePareamentoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// A frase diz o que fazer, nao so o que aconteceu.
    ///
    /// O stub existe porque o Baileys esta desligado por configuracao, e a
    /// acao depende do motivo: pode ser o Node parado no Render, ou o secret
    /// sem Habilitado. Sem esta frase, a tela mostraria "parado" e a
    /// administradora nao saberia nem por onde comecar.
    /// </summary>
    private const string WhatsAppDesligado =
        "O WhatsApp esta desligado por configuracao. Confira se Habilitado e BAILEYS_SEGREDO_COMPARTILHADO " +
        "estao no secret do GitHub, e se o servico do Node esta no ar no Render.";
}