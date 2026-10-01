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

    /// <summary>
    /// Situacao do numero da loja. Serve para o painel mostrar se o WhatsApp
    /// esta pareado, e nao para o envio: <see cref="EnviarMensagemAsync"/>
    /// pergunta isso para si mesmo e devolve <c>false</c>.
    /// </summary>
    Task<StatusDoWhatsApp> ObterStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// QR de pareamento, em PNG base64, ou <c>null</c> se ainda nao houver.
    ///
    /// O segredo nunca chega ao navegador. O painel pede o QR a API, a API
    /// repassa: e o que impede que o QR do numero da loja, que e o proprio
    /// acesso a conta, fique exposto em qualquer pagina aberta pelo navegador.
    /// </summary>
    Task<string?> ObterQrDePareamentoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma nova tentativa de conexao e devolve o QR, se ja houver.
    ///
    /// Existe porque ler o QR atual nao adianta: entre uma tentativa e outra
    /// nao existe socket, entao nao existe QR para reler. Sem esta rota, o
    /// botao "Gerar novo QR" da tela nao faria nada e pareceria quebrado.
    /// </summary>
    Task<string?> ForcarReconexaoDePareamentoAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Como esta o WhatsApp da loja, para o painel.
/// </summary>
/// <param name="ServicoNoAr">O Node respondeu. False significa Node parado.</param>
/// <param name="Pareado">O numero foi pareado e a sessao esta aberta.</param>
/// <param name="Numero">Numero conectado, quando houver.</param>
/// <param name="Nome">Nome do perfil, quando houver.</param>
/// <param name="ConectadoDesde">Desde quando a sessao esta aberta.</param>
/// <param name="UltimoEnvioEm">Ultimo envio que deu certo.</param>
/// <param name="Motivo">
/// Quando o servico nao esta no ar, a causa em linguagem de acao: "a pasta do
/// Node nao existe" ou "falta o segredo". Sem isso a tela mostraria so
/// "parado", e sem console no servidor nao haveria como descobrir o motivo.
/// </param>
public sealed record StatusDoWhatsApp(
    bool ServicoNoAr,
    bool Pareado,
    string? Numero,
    string? Nome,
    DateTime? ConectadoDesde,
    DateTime? UltimoEnvioEm,
    string? Motivo);

public interface IFocusNfeService
{
    Task<string> EmitirNotaAsync(long pedidoId, CancellationToken cancellationToken = default);
}

public interface IViaCepService
{
    Task<ResultadoViaCep?> ConsultarAsync(string cep, CancellationToken cancellationToken = default);
}

public interface IGeocodificador
{
    /// <summary>
    /// Coordenadas do endereço, ou nulo quando nenhum provedor localiza.
    ///
    /// Nulo é recusado pelo cálculo, e não virado para um ponto qualquer: um
    /// frete inventado custa dinheiro e confiança do cliente.
    /// </summary>
    Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default);

    /// <summary>
    /// Por que a última busca não achou, para o diagnóstico distinguir "o mapa
    /// não tem este CEP" de "não foi possível falar com o mapa".
    ///
    /// As duas causas precisam de consertos diferentes: um CEP sem ponto se
    /// resolve no mapa, uma falha de rede se resolve no servidor. Sem isto, a
    /// tela mostraria "não conseguimos localizar o endereço" para as duas, e o
    /// diagnóstico vira chute.
    /// </summary>
    string? UltimaFalha { get; }

    /// <summary>
    /// Consulta cada provedor e devolve o que cada um respondeu.
    ///
    /// Existe porque o servidor de produção não fala TLS com o Nominatim, e a
    /// única forma de saber se os substitutos funcionam de lá é chamá-los de
    /// lá — medir da estação de trabalho não vale, como já mostrou.
    /// </summary>
    Task<IReadOnlyList<object>> TestarProvedoresAsync(string cep, CancellationToken cancellationToken = default);
}

public interface INominatimService
{
    Task<(decimal Latitude, decimal Longitude)?> GeocodificarAsync(string endereco, CancellationToken cancellationToken = default);

    /// <summary>
    /// Por que a última busca devolveu nada: o mapa não tem o CEP, ou o
    /// servidor não conseguiu falar com o mapa.
    ///
    /// O cálculo não precisa saber disso - para ele, os dois casos são "sem
    /// coordenada". Quem precisa é quem diagnostica, porque as correções são
    /// diferentes: um CEP sem ponto se resolve no mapa, uma falha de rede se
    /// resolve no servidor.
    ///
    /// O nome é <c>UltimoFalha</c>, sem acento, e o mesmo que o
    /// <c>IGeocodificador</c> expõe. As duas interfaces entram na mesma tela de
    /// diagnóstico, e dois nomes para a mesma coisa fariam o painel mostrar o
    /// motivo em um caso e nada no outro.
    /// </summary>
    string? UltimoFalha { get; }

    /// <summary>
    /// O mesmo cliente que a geocodificação usa, para o diagnóstico exercitar
    /// a conexão que de fato falha - e não uma configuração paralela.
    /// </summary>
    HttpClient CriarClienteDeDiagnostico();
}

public sealed record ResultadoViaCep(
    string Cep,
    string Logradouro,
    string Bairro,
    string Cidade,
    string Estado);