namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Com quem e por onde a loja fala quando um pedido nasce.
///
/// Fica em configuração e não no código porque o e-mail da administradora e o
/// número dela são dados dela, e não regra da loja: trocar de número não é
/// mudança de código.
/// </summary>
public sealed class NotificacoesDePedidoOptions
{
    /// <summary>Quem recebe o aviso de pedido novo, para dar o aceite.</summary>
    public string EmailDaAdministradora { get; set; } = string.Empty;

    /// <summary>
    /// Número de onde sai a mensagem de WhatsApp. Não é usado para enviar — quem
    /// envia é a sessão do Baileys conectada — e sim para deixar registrado no log
    /// qual número falhou, porque a sessão pode estar conectada em outro aparelho.
    /// </summary>
    public string WhatsAppDaAdministradora { get; set; } = string.Empty;

    public string NomeDaLoja { get; set; } = "Lumi Makeup";

    /// <summary>
    /// Quando true, o cliente é avisado também por WhatsApp. Existe para desligar
    /// a mensagem sem recompilar, já que o número da administradora muda com
    /// frequência e a sessão do Baileys ainda está por vir.
    /// </summary>
    public bool AvisarPorWhatsApp { get; set; } = true;
}
