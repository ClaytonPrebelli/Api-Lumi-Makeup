namespace LumiMakeup.Application.Abstractions;

public interface IEmailSender
{
    Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default);
}

public interface IRecaptchaValidator
{
    Task<bool> ValidarTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface ICloudinaryService
{
    Task<string> EnviarAsync(Stream arquivo, string nomeDoArquivo, string pasta, CancellationToken cancellationToken = default);
    Task ExcluirAsync(string idPublico, CancellationToken cancellationToken = default);
}

public interface IWhatsAppService
{
    Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken cancellationToken = default);
}

public interface IFocusNfeService
{
    Task<string> EmitirNotaAsync(long pedidoId, CancellationToken cancellationToken = default);
}

public interface IViaCepService
{
    Task<ResultadoViaCep?> ConsultarAsync(string cep, CancellationToken cancellationToken = default);
}

public interface INominatimService
{
    Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default);
}

public sealed record ResultadoViaCep(
    string Cep,
    string Logradouro,
    string Bairro,
    string Cidade,
    string Estado);