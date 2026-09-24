namespace LumiMakeup.Aplicacao.Abstracoes;

public interface IRemetenteDeEmail
{
    Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default);
}

public interface IValidadorDeRecaptcha
{
    Task<bool> ValidarTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IServicoCloudinary
{
    Task<string> EnviarAsync(Stream arquivo, string nomeDoArquivo, string pasta, CancellationToken cancellationToken = default);
    Task ExcluirAsync(string idPublico, CancellationToken cancellationToken = default);
}

public interface IServicoDeWhatsApp
{
    Task<bool> EnviarMensagemAsync(string telefone, string mensagem, CancellationToken cancellationToken = default);
}

public interface IServicoFocusNfe
{
    Task<string> EmitirNotaAsync(long pedidoId, CancellationToken cancellationToken = default);
}

public interface IServicoViaCep
{
    Task<ResultadoViaCep?> ConsultarAsync(string cep, CancellationToken cancellationToken = default);
}

public interface IServicoNominatim
{
    Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default);
}

public sealed record ResultadoViaCep(
    string Cep,
    string Logradouro,
    string Bairro,
    string Cidade,
    string Estado);