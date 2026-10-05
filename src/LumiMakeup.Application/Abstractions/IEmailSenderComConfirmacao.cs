namespace LumiMakeup.Application.Abstractions;

public interface IEmailSenderComConfirmacao
{
    Task<bool> EnviarComConfirmacaoAsync(
        string destino,
        string assunto,
        string corpoHtml,
        CancellationToken cancellationToken = default);
}
