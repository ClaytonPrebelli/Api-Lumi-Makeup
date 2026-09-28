namespace LumiMakeup.Application.Abstractions;

public interface IEmailSender
{
    Task EnviarAsync(string destino, string assunto, string corpoHtml, CancellationToken cancellationToken = default);
}

public interface IRecaptchaValidator
{
    Task<bool> ValidarTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IArmazenamentoDeImagens
{
    /// <summary>Grava a imagem na <c>PastaPadrao</c> da configuração.</summary>
    Task<ImagemArmazenada> ArmazenarAsync(Stream conteudo, string nomeOriginal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a imagem numa subpasta própria dentro de <c>CaminhoBase</c>.
    ///
    /// É um método separado, e não um parâmetro opcional em
    /// <see cref="ArmazenarAsync"/>, por dois motivos: acrescentar um parâmetro
    /// opcional a uma interface já implementada quebra todo chamador de Moq
    /// (árvore de expressão não aceita argumento omitido), e o nome fica mais
    /// honesto — quem chama diz em qual pasta quer gravar.
    ///
    /// Sem isso, os banners do hero acabariam gravados dentro de
    /// <c>produtos/</c>, misturados com as fotos dos produtos.
    /// </summary>
    Task<ImagemArmazenada> ArmazenarEmPastaAsync(
        Stream conteudo,
        string nomeOriginal,
        string pasta,
        CancellationToken cancellationToken = default);

    Task ExcluirAsync(string caminhoRelativo, CancellationToken cancellationToken = default);
}

public sealed record ImagemArmazenada(
    string CaminhoRelativo,
    string NomeOriginal,
    string ContentType,
    long TamanhoEmBytes);

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