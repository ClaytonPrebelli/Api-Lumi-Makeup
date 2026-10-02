namespace LumiMakeup.Infrastructure.Integrations;

/// <summary>
/// Com quem e por onde a loja fala quando um pedido nasce.
///
/// Fica em configura��ǜo e nǜo no c��digo porque o e-mail da administradora e o
/// nǧmero dela sǜo dados dela, e nǜo regra da loja: trocar de nǧmero nǜo Ǹ
/// mudan��a de c��digo.
/// </summary>
public sealed class NotificacoesDePedidoOptions
{
    /// <summary>Quem recebe o aviso de pedido novo, para dar o aceite.</summary>
    public string EmailDaAdministradora { get; set; } = string.Empty;

    /// <summary>
    /// Nǧmero de onde sai a mensagem de WhatsApp. Nǜo Ǹ usado para enviar �?" quem
    /// envia Ǹ a sessǜo do Baileys conectada �?" e sim para deixar registrado no log
    /// qual nǧmero falhou, porque a sessǜo pode estar conectada em outro aparelho.
    /// </summary>
    public string WhatsAppDaAdministradora { get; set; } = string.Empty;

    public string NomeDaLoja { get; set; } = "Lumi Makeup";

    /// <summary>
    /// Mensagem inicial para o cliente por WhatsApp, antes dos itens do pedido.
    /// Pode ser customizada pelo painel administrativo.
    /// </summary>
    public string MensagemInicialWhatsAppCliente { get; set; } = "Oi, {Nome}! Aqui é da {Loja}. Recebemos seu pedido!";

    /// <summary>
    /// Quando true, o cliente Ǹ avisado tambǸm por WhatsApp. Existe para desligar
    /// a mensagem sem recompilar, jǭ que o nǧmero da administradora muda com
    /// frequǦncia e a sessǜo do Baileys ainda estǭ por vir.
    /// </summary>
    public bool AvisarPorWhatsApp { get; set; } = true;
}
