namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// O que o supervisor descobriu sobre o Node, para o painel conseguir explicar.
///
/// Existe por causa de uma limitacao do servidor: o acesso e so por FTP, sem
/// console. O Node pode nao subir por cinco motivos diferentes - pasta errada,
/// executavel faltando, script faltando, segredo ausente, processo morreu - e
/// sem este registro a administradora so veria "servico parado", sem pista de
/// qual deles foi.
///
/// O supervisor escreve; o painel le. Ninguem chama o Node para descobrir isso,
/// porque quando o Node nao subiu nao ha nada para perguntar a ele.
/// </summary>
public sealed class EstadoDoNodeBaileys
{
    private readonly object _travamento = new();
    private string? _ultimoProblema;

    /// <summary>True quando o supervisor esta ligado por configuracao.</summary>
    public bool SupervisorAtivo { get; private set; }

    /// <summary>Pasta onde o supervisor procura o Node.</summary>
    public string PastaDoNode { get; private set; } = string.Empty;

    /// <summary>Ultimo problema encontrado, ou null se o Node subiu.</summary>
    public string? UltimoProblema
    {
        get
        {
            lock (_travamento)
            {
                return _ultimoProblema;
            }
        }
    }

    public void RegistrarSupervisorAtivo(string pastaDoNode)
    {
        lock (_travamento)
        {
            SupervisorAtivo = true;
            PastaDoNode = pastaDoNode;
        }
    }

    public void RegistrarProblema(string problema)
    {
        lock (_travamento)
        {
            _ultimoProblema = problema;
        }
    }

    public void RegistrarNodeNoAr()
    {
        lock (_travamento)
        {
            _ultimoProblema = null;
        }
    }

    /// <summary>
    /// Frase que a administradora le na tela.
    ///
    /// Ela existe porque "o servico esta parado" sem causa nao ajuda ninguem a
    /// agir: a acao depende do motivo. Pasta errada e problema de FTP;
    /// segredo ausente e problema no GitHub.
    /// </summary>
    public string? Explicacao()
    {
        var problema = UltimoProblema;

        if (problema is not null)
        {
            return problema;
        }

        if (SupervisorAtivo)
        {
            return null;
        }

        // O supervisor desligado NAO e problema: e o estado normal desde que o
        // Node foi para o Render. A hospedagem compartilhada nao roda Node, e a
        // API nao deveria tentar.
        //
        // A mensagem antiga mandava ligar BAILEYS_INICIAR_PROCESSO, o que seria
        // PIOR: a API ficaria procurando um node.exe que nao existe na
        // hospedagem, e o sintomo continuaria sendo o mesmo.
        return "O Node do WhatsApp nao respondeu. Ele roda no Render, nao neste servidor: " +
               "confirme se o servico de la esta ativo e se URL_DA_API aponta para o endereco certo.";
    }
}
