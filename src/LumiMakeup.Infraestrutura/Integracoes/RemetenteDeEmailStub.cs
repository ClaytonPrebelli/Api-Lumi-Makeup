using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.Extensions.Logging;

namespace LumiMakeup.Infraestrutura.Integracoes;

public sealed class RemetenteDeEmailStub : IRemetenteDeEmail
{
    private readonly ILogger<RemetenteDeEmailStub> _logger;

    public RemetenteDeEmailStub(ILogger<RemetenteDeEmailStub> logger)
    {
        _logger = logger;
    }

    public Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "RemetenteDeEmailStub: e-mail NÃO enviado (integração Brevo pendente). Para={Destino} Assunto={Assunto}",
            destino,
            assunto);
        return Task.CompletedTask;
    }
}