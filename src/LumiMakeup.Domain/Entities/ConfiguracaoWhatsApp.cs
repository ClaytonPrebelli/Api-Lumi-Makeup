namespace LumiMakeup.Domain.Entities;

public class ConfiguracaoWhatsApp
{
    public long Id { get; set; }
    public string MensagemInicialCliente { get; set; } = "Oi, {Nome}! Aqui é da {Loja}. Recebemos seu pedido!";
}
