namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Onde esta o Node do Baileys e como a API fala com ele.
///
/// Fica em configuracao porque o caminho muda entre a maquina de desenvolvimento
/// e o servidor: em dev o Node roda na mao, e em producao ele e uma pasta irma
/// da publicacao, iniciada por esta API.
///
/// A decisao de subir o Node e da API, e nao de um servico do Windows, porque o
/// servidor de producao so recebe arquivo por FTP: nao ha console para instalar
/// Node, nem Task Scheduler para agendar inicio. Quem ja esta rodando e quem
/// sabe reiniciar.
/// </summary>
public sealed class OpcoesDeBaileys
{
    /// <summary>
    /// Liga o envio por WhatsApp. Desligado, a API registra o stub e o site
    /// continua inteiro: o Node e acessorio, nao pode derrubar a loja.
    /// </summary>
    public bool Habilitado { get; set; }

    /// <summary>Endereco do Node. Fica em localhost: ele nunca e exposto.</summary>
    public string UrlBase { get; set; } = "http://127.0.0.1:3001";

    /// <summary>
    /// Senha que a API manda no cabecalho <c>x-segredo</c>. Vem do GitHub
    /// Actions no deploy e nunca do arquivo versionado.
    /// </summary>
    public string SegredoCompartilhado { get; set; } = string.Empty;

    /// <summary>
    /// Pasta do Node. Relativa a raiz da publicacao, porque a publicacao e
    /// plana e o Node fica na pasta irma:
    /// <c>api.lumimakeup.com.br/</c> e <c>whats.lumimakeup.com.br/</c>.
    /// </summary>
    public string PastaDoNode { get; set; } = string.Empty;

    /// <summary>
    /// Caminho do node.exe, relativo a <see cref="PastaDoNode"/>. Aponta para
    /// o executavel, e nao para "node" do PATH: em producao nao ha PATH de
    /// usuario nem Node instalado, e o que existe e a copia portatil.
    /// </summary>
    public string Executavel { get; set; } = "node.exe";

    /// <summary>Porta que o Node escuta. Tem que ser a mesma em UrlBase.</summary>
    public int Porta { get; set; } = 3001;

    /// <summary>Desliga o supervisor quando se roda o Node na mao, em desenvolvimento.</summary>
    public bool IniciarProcesso { get; set; }

    /// <summary>Quanto esperar o Node responder depois de comecar.</summary>
    public int EsperaDeSubidaEmSegundos { get; set; } = 40;

    /// <summary>Intervalo entre uma queda e a tentativa de subir de novo.</summary>
    public int IntervaloDeReinicioEmSegundos { get; set; } = 5;

    /// <summary>Timeout de cada envio. Baixo de proposito: o pedido nao pode esperar.</summary>
    public int TimeoutDoEnvioEmSegundos { get; set; } = 15;
}
