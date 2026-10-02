using System.Globalization;
using System.Text;
using LumiMakeup.Application.DTOs;

namespace LumiMakeup.Infrastructure.Services;

/// <summary>
/// Monta o texto da mensagem de WhatsApp que o cliente recebe na confirmação do
/// pedido.
///
/// Fica separado do <c>NotificadorDePedido</c> porque a tela de configuração do
/// painel precisa mostrar a prévia, e a prévia só vale alguma coisa se for
/// montada pelo mesmo código que monta a mensagem real. Duas implementações
/// divergem na primeira frase que a administradora trocar.
///
/// O WhatsApp renderiza negrito no texto com <c>*</c> simples, e não com
/// <c>**</c> como o Markdown. Os títulos saem com asterisco único para
/// chegarem em negrito de verdade no aparelho do cliente.
/// </summary>
public static class MontadorDeMensagemDePedido
{
    /// <summary>
    /// Os marcadores aceitos dentro da frase inicial da administradora.
    /// </summary>
    public const string MarcadorDeNome = "{Nome}";

    public const string MarcadorDeLoja = "{Loja}";

    /// <summary>
    /// Pedido de exemplo, só para a prévia. Serve para a administradora ver o
    /// tamanho da mensagem, os títulos em negrito e onde os valores caem, antes
    /// de gravar a frase.
    /// </summary>
    public static PedidoDto PedidoDeExemplo() => new(
        0,
        0,
        "Maria Souza",
        null,
        "11999999999",
        "maria@exemplo.com",
        Domain.Enums.OrigemPedido.Online,
        Domain.Enums.StatusPedido.AguardandoPagamento,
        null,
        null,
        81.80m,
        0m,
        10.00m,
        91.80m,
        null,
        new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
        null,
        "01310-000",
        "Avenida Paulista",
        "1000",
        "Conj. 101",
        "Bela Vista",
        "São Paulo",
        "SP",
        [
            new PedidoItemDto(1, "Batom Matte", 1, 35.90m, null, 35.90m),
            new PedidoItemDto(2, "Pó Compacto", 1, 45.90m, null, 45.90m)
        ]);

    /// <summary>
    /// Troca os marcadores pelo dado real do pedido. Marcador que não existe
    /// some em silêncio, e não quebra a mensagem no meio do envio.
    /// </summary>
    public static string ResolverMarcadores(
        string frase,
        string nomeDoCliente,
        string nomeDaLoja)
    {
        return (frase ?? string.Empty)
            .Replace(MarcadorDeNome, PrimeiroNome(nomeDoCliente), StringComparison.Ordinal)
            .Replace(MarcadorDeLoja, nomeDaLoja, StringComparison.Ordinal);
    }

    public static string Montar(string mensagemInicial, PedidoDto pedido, string nomeDaLoja)
    {
        var texto = new StringBuilder();

        texto.AppendLine(ResolverMarcadores(mensagemInicial, pedido.NomeCliente, nomeDaLoja));
        texto.AppendLine();
        texto.AppendLine("*Itens do Pedido*");

        foreach (var item in pedido.Itens)
        {
            texto.AppendLine($"• {item.Quantidade}x {item.Nome} — {Moeda(item.Subtotal)}");
        }

        texto.AppendLine();
        texto.AppendLine($"*Subtotal:* {Moeda(pedido.Subtotal)}");

        if (pedido.Desconto > 0)
        {
            var cupom = string.IsNullOrWhiteSpace(pedido.CupomCodigo)
                ? string.Empty
                : $" ({pedido.CupomCodigo})";

            texto.AppendLine($"*Desconto{cupom}:* {Moeda(pedido.Desconto)}");
        }

        texto.AppendLine($"*Frete:* {Moeda(pedido.CustoFrete)}");
        texto.AppendLine($"*Total:* {Moeda(pedido.Total)}");

        var endereco = MontarEndereco(pedido);

        if (endereco is not null)
        {
            texto.AppendLine();
            texto.AppendLine(endereco);
        }

        texto.AppendLine();
        texto.AppendLine(
            "Confirme seu pedido acima se está tudo certo por favor. É só responder esta mensagem.");

        return texto.ToString().Trim();
    }

    private static string? MontarEndereco(PedidoDto pedido)
    {
        // Pedido de balcão não tem endereço, e a mensagem não pode inventar um.
        // Sem entrega, o bloco inteiro é omitido.
        if (string.IsNullOrWhiteSpace(pedido.EnderecoLogradouro))
        {
            return null;
        }

        var numero = string.IsNullOrWhiteSpace(pedido.EnderecoNumero)
            ? string.Empty
            : $", {pedido.EnderecoNumero}";

        var complemento = string.IsNullOrWhiteSpace(pedido.EnderecoComplemento)
            ? string.Empty
            : $" - {pedido.EnderecoComplemento}";

        var bairro = string.IsNullOrWhiteSpace(pedido.EnderecoBairro)
            ? string.Empty
            : $"{pedido.EnderecoBairro} - ";

        var cidade = string.IsNullOrWhiteSpace(pedido.EnderecoEstado)
            ? pedido.EnderecoCidade ?? string.Empty
            : $"{pedido.EnderecoCidade}/{pedido.EnderecoEstado}";

        var cep = string.IsNullOrWhiteSpace(pedido.EnderecoCep)
            ? string.Empty
            : $"\nCEP: {pedido.EnderecoCep}";

        return $"*Endereço de entrega:*\n" +
               $"{pedido.EnderecoLogradouro}{numero}{complemento}\n" +
               $"{bairro}{cidade}{cep}";
    }

    private static string PrimeiroNome(string nome) =>
        nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    private static string Moeda(decimal valor) =>
        valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
}