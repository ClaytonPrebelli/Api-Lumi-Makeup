using System.Globalization;
using System.Net;
using System.Text;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Infrastructure.Services;

/// <summary>
/// Avisa sobre o pedido em três canais: e-mail ao cliente, e-mail à administradora e
/// WhatsApp ao cliente, saindo do número dela.
///
/// Nenhum aviso pode derrubar o pedido. Por isso cada envio é isolado em seu
/// próprio <c>try</c>: e-mail do cliente falhando não pode impedir o e-mail da
/// administradora, que é o que dispara a ação comercial. E a falha toda fica no
/// log, porque "não enviou" sem motivo é impossível de investigate depois.
/// </summary>
public sealed class NotificadorDePedido : INotificadorDePedido
{
    private readonly IEmailSender _email;
    private readonly IWhatsAppService _whatsApp;
    private readonly NotificacoesDePedidoOptions _opcoes;
    private readonly ILogger<NotificadorDePedido> _logger;

    public NotificadorDePedido(
        IEmailSender email,
        IWhatsAppService whatsApp,
        IOptions<NotificacoesDePedidoOptions> opcoes,
        ILogger<NotificadorDePedido> logger)
    {
        _email = email;
        _whatsApp = whatsApp;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    public async Task PedidoCriadoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        // A ordem aqui é deliberada: o e-mail da administradora vem primeiro,
        // porque é o que inicia a ação comercial. Se o canal do cliente falhar, o
        // pedido ainda precisa chegar a quem pode fechar a venda.
        await AvisarAdministradoraAsync(pedido, cancellationToken);
        await AvisarClientePorEmailAsync(pedido, "Recebemos seu pedido", cancellationToken);
        await AvisarClientePorWhatsAppAsync(pedido, cancellationToken);
    }

    public async Task PedidoPagoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        await AvisarClientePorEmailAsync(pedido, "Seu pedido foi confirmado", cancellationToken);
        await AvisarClientePorWhatsAppAsync(pedido, cancellationToken);
    }

    public async Task PedidoCanceladoAsync(PedidoDto pedido, CancellationToken cancellationToken = default)
    {
        await AvisarClientePorEmailAsync(pedido, "Seu pedido foi cancelado", cancellationToken);
        await AvisarClientePorWhatsAppAsync(pedido, cancellationToken);
    }

    private async Task AvisarAdministradoraAsync(PedidoDto pedido, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.EmailDaAdministradora))
        {
            // Sem este registro, um e-mail da admin não configurado aparece como
            // "nenhum pedido chegou", que é o pior sintoma possível: a venda some
            // sem nenhum sinal.
            _logger.LogError(
                "Pedido {PedidoId} criado, mas NotificacoesDePedido:EmailDaAdministradora nao esta configurada. Nenhum e-mail de aviso foi enviado.",
                pedido.Id);

            return;
        }

        try
        {
            await _email.EnviarAsync(
                _opcoes.EmailDaAdministradora,
                $"Pedido {pedido.Id} de {pedido.NomeCliente}",
                CorpoDoPedidoParaAdministradora(pedido),
                cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao avisar a administradora do pedido {PedidoId}.", pedido.Id);
        }
    }

    private async Task AvisarClientePorEmailAsync(PedidoDto pedido, string assunto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pedido.EmailContato))
        {
            _logger.LogWarning(
                "Pedido {PedidoId} sem e-mail de contato. O cliente nao foi avisado por e-mail.",
                pedido.Id);

            return;
        }

        try
        {
            await _email.EnviarAsync(pedido.EmailContato, $"{assunto} - {_opcoes.NomeDaLoja}", CorpoDoPedido(pedido, assunto), cancellationToken);
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao avisar o cliente do pedido {PedidoId} por e-mail.", pedido.Id);
        }
    }

    private async Task AvisarClientePorWhatsAppAsync(PedidoDto pedido, CancellationToken cancellationToken)
    {
        if (!_opcoes.AvisarPorWhatsApp)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(pedido.TelefoneContato))
        {
            // Cliente sem telefone não tem por onde ser avisado. Isso é motivo para
            // a administradora olhar o pedido na mão, e não motivo para falhar.
            _logger.LogWarning(
                "Pedido {PedidoId} sem telefone. O cliente nao foi avisado por WhatsApp e precisa de contato por outro canal.",
                pedido.Id);

            return;
        }

        try
        {
            var numero = NormalizarTelefone(pedido.TelefoneContato);

            if (numero is null)
            {
                _logger.LogWarning(
                    "Telefone {Telefone} do pedido {PedidoId} nao parece um numero valido. WhatsApp nao enviado.",
                    pedido.TelefoneContato,
                    pedido.Id);

                return;
            }

            var enviada = await _whatsApp.EnviarMensagemAsync(numero, MensagemDeWhatsApp(pedido), cancellationToken);

            if (!enviada)
            {
                _logger.LogWarning(
                    "WhatsApp do pedido {PedidoId} nao foi enviado (de {NumeroDaAdmin} para {Numero}).",
                    pedido.Id,
                    _opcoes.WhatsAppDaAdministradora,
                    numero);
            }
        }
        catch (Exception excecao)
        {
            _logger.LogError(excecao, "Falha ao avisar o cliente do pedido {PedidoId} por WhatsApp.", pedido.Id);
        }
    }

    /// <summary>
    /// Deixa só dígitos, com o código do Brasil quando o número tem 10 ou 11
    /// dígitos. Sem isso, o Baileys receberia o número como foi digitado no
    /// cadastro e a mensagem não sairia.
    /// </summary>
    public static string? NormalizarTelefone(string telefone)
    {
        var digitos = new string(telefone.Where(char.IsDigit).ToArray());

        return digitos.Length switch
        {
            10 or 11 => $"55{digitos}",
            12 or 13 when digitos.StartsWith("55", StringComparison.Ordinal) => digitos,
            _ => null
        };
    }

    /// <summary>
    /// A mensagem vai do número da administradora para o cliente, e é ela que
    /// recebe a resposta e fecha a venda. Por isso o texto fala da loja em
    /// primeira pessoa e não parece um disparo automático.
    /// </summary>
    private string MensagemDeWhatsApp(PedidoDto pedido)
    {
        var texto = new StringBuilder();

        var mensagemInicial = _opcoes.MensagemInicialWhatsAppCliente
            .Replace("{Nome}", PrimeiroNome(pedido.NomeCliente))
            .Replace("{Loja}", _opcoes.NomeDaLoja);

        texto.AppendLine(mensagemInicial);
        texto.AppendLine();
        texto.AppendLine("**Itens do Pedido:**");
        texto.AppendLine();

        foreach (var item in pedido.Itens)
        {
            texto.AppendLine($"• {item.Quantidade}x {item.Nome} - {Moeda(item.Subtotal)}");
        }

        texto.AppendLine();
        texto.AppendLine("**Subtotal:** " + Moeda(pedido.Subtotal));
        if (pedido.Desconto > 0)
        {
            var cupom = string.IsNullOrWhiteSpace(pedido.CupomCodigo) ? string.Empty : $" ({pedido.CupomCodigo})";
            texto.AppendLine($"**Desconto{cupom}:** " + Moeda(pedido.Desconto));
        }
        texto.AppendLine("**Frete:** " + Moeda(pedido.CustoFrete));
        texto.AppendLine("**Total:** " + Moeda(pedido.Total));
        texto.AppendLine();

        if (!string.IsNullOrWhiteSpace(pedido.EnderecoLogradouro))
        {
            texto.AppendLine("**Endereço de entrega:**");
            var complemento = string.IsNullOrWhiteSpace(pedido.EnderecoComplemento)
                ? string.Empty
                : " - " + pedido.EnderecoComplemento;
            texto.AppendLine($"{pedido.EnderecoLogradouro}, {pedido.EnderecoNumero ?? string.Empty}{complemento}");
            texto.AppendLine($"{pedido.EnderecoBairro ?? string.Empty} - {pedido.EnderecoCidade ?? string.Empty}/{pedido.EnderecoEstado ?? string.Empty}");
            texto.AppendLine($"CEP: {pedido.EnderecoCep ?? string.Empty}");
            texto.AppendLine();
        }

        texto.AppendLine("Confirme seu pedido acima se está tudo certo por favor. É só responder esta mensagem com 'OK' ou qualquer outra coisa.");

        return texto.ToString().Trim();
    }

    private static string CorpoDoPedidoParaAdministradora(PedidoDto pedido)
    {
        var origem = pedido.Origem is Domain.Enums.OrigemPedido.Balcao
            ? "Venda de balcão"
            : "Pedido online";

        var itens = new StringBuilder();
        itens.Append("<table cellpadding=\"8\" cellspacing=\"0\" style=\"border-collapse:collapse; width:100%; border:1px solid #e0e0e0; border-radius:8px;\">");
        itens.Append("<thead><tr style=\"background:#f8f3ef; border-bottom:1px solid #e0e0e0;\"><th align=\"left\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Item</th><th align=\"right\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Qtd</th><th align=\"right\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Unit.</th><th align=\"right\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Subtotal</th></tr></thead>");
        itens.Append("<tbody>");

        foreach (var item in pedido.Itens)
        {
            itens.Append("<tr>");
            itens.Append($"<td style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{Escapar(item.Nome)}</td>");
            itens.Append($"<td align=\"right\" style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{item.Quantidade}</td>");
            itens.Append($"<td align=\"right\" style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{Moeda(item.PrecoPromocionalUnitario ?? item.PrecoVendaUnitario)}</td>");
            itens.Append($"<td align=\"right\" style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{Moeda(item.Subtotal)}</td>");
            itens.Append("</tr>");
        }

        itens.Append("</tbody></table>");

        var enderecoHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(pedido.EnderecoLogradouro))
        {
            enderecoHtml = $"""
                <div style="margin-top:16px; padding:16px; background:#f9f9f9; border-radius:8px; border:1px solid #e0e0e0;">
                    <h3 style="margin:0 0 8px 0; font-size:14px; color:#5a4a3f;">Endereço de entrega</h3>
                    <p style="margin:0; font-size:14px; color:#333333; line-height:1.6;">
                        {Escapar(pedido.EnderecoLogradouro)}, {Escapar(pedido.EnderecoNumero ?? string.Empty)}
                        {(string.IsNullOrWhiteSpace(pedido.EnderecoComplemento) ? string.Empty : " - " + Escapar(pedido.EnderecoComplemento))}<br />
                        {Escapar(pedido.EnderecoBairro ?? string.Empty)} - {Escapar(pedido.EnderecoCidade ?? string.Empty)}/{Escapar(pedido.EnderecoEstado ?? string.Empty)}<br />
                        CEP {Escapar(pedido.EnderecoCep ?? string.Empty)}
                    </p>
                </div>
                """;
        }

        return $"""
            <div style="font-family:Arial, Helvetica, sans-serif; color:#333333; max-width:600px;">
                <h2 style="color:#b98b73; margin-bottom:8px;">Novo pedido recebido</h2>
                <p>Um {origem.ToLowerInvariant()} foi registrado e está aguardando pagamento.</p>
                <p><strong>Pedido:</strong> {pedido.Id}<br />
                <strong>Cliente:</strong> {Escapar(pedido.NomeCliente)}<br />
                <strong>Telefone:</strong> {Escapar(pedido.TelefoneContato ?? "não informado")}<br />
                <strong>E-mail:</strong> {Escapar(pedido.EmailContato ?? "não informado")}<br />
                <strong>Recebido em:</strong> {pedido.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"))}</p>
                {itens}
                <div style="margin-top:16px; padding:16px; background:#f8f3ef; border-radius:8px; border:1px solid #e0e0e0;">
                    <p style="margin:0; font-size:14px; line-height:1.8;">
                        <strong>Subtotal:</strong> {Moeda(pedido.Subtotal)}<br />
                        {(pedido.Desconto > 0 ? $"<strong>Desconto{(pedido.CupomCodigo is null ? "" : $" ({Escapar(pedido.CupomCodigo)})")}:</strong> {Moeda(pedido.Desconto)}<br />" : string.Empty)}
                        <strong>Frete:</strong> {Moeda(pedido.CustoFrete)}<br />
                        <strong>Total:</strong> {Moeda(pedido.Total)}
                    </p>
                </div>
                {enderecoHtml}
                <p style="margin-top:16px; color:#666666; font-size:13px;">A nota fiscal deste pedido ainda não foi gerada.</p>
            </div>
            """;
    }

    private static string CorpoDoPedido(PedidoDto pedido, string assunto)
    {
        var itens = new StringBuilder();
        itens.Append("<table cellpadding=\"8\" cellspacing=\"0\" style=\"border-collapse:collapse; width:100%; border:1px solid #e0e0e0; border-radius:8px;\">");
        itens.Append("<thead><tr style=\"background:#f8f3ef; border-bottom:1px solid #e0e0e0;\"><th align=\"left\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Item</th><th align=\"right\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Qtd</th><th align=\"right\" style=\"padding:12px; font-size:14px; color:#5a4a3f;\">Subtotal</th></tr></thead>");
        itens.Append("<tbody>");

        foreach (var item in pedido.Itens)
        {
            itens.Append("<tr>");
            itens.Append($"<td style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{Escapar(item.Nome)}</td>");
            itens.Append($"<td align=\"right\" style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{item.Quantidade}</td>");
            itens.Append($"<td align=\"right\" style=\"padding:12px; border-bottom:1px solid #f0f0f0; font-size:14px; color:#333333;\">{Moeda(item.Subtotal)}</td>");
            itens.Append("</tr>");
        }

        itens.Append("</tbody></table>");

        var enderecoHtml = string.Empty;
        if (!string.IsNullOrWhiteSpace(pedido.EnderecoLogradouro))
        {
            enderecoHtml = $"""
                <div style="margin-top:16px; padding:16px; background:#f9f9f9; border-radius:8px; border:1px solid #e0e0e0;">
                    <h3 style="margin:0 0 8px 0; font-size:14px; color:#5a4a3f;">Endereço de entrega</h3>
                    <p style="margin:0; font-size:14px; color:#333333; line-height:1.6;">
                        {Escapar(pedido.EnderecoLogradouro)}, {Escapar(pedido.EnderecoNumero ?? string.Empty)}
                        {(string.IsNullOrWhiteSpace(pedido.EnderecoComplemento) ? string.Empty : " - " + Escapar(pedido.EnderecoComplemento))}<br />
                        {Escapar(pedido.EnderecoBairro ?? string.Empty)} - {Escapar(pedido.EnderecoCidade ?? string.Empty)}/{Escapar(pedido.EnderecoEstado ?? string.Empty)}<br />
                        CEP {Escapar(pedido.EnderecoCep ?? string.Empty)}
                    </p>
                </div>
                """;
        }

        var aguardando = pedido.Status is Domain.Enums.StatusPedido.AguardandoPagamento;

        return $"""
            <div style="font-family:Arial, Helvetica, sans-serif; color:#333333; max-width:600px;">
                <h2 style="color:#b98b73; margin-bottom:8px;">{Escapar(assunto)}</h2>
                <p>Olá, {Escapar(PrimeiroNome(pedido.NomeCliente))}!</p>
                <p>O número do seu pedido é <strong>{pedido.Id}</strong>.</p>
                {itens}
                <div style="margin-top:16px; padding:16px; background:#f8f3ef; border-radius:8px; border:1px solid #e0e0e0;">
                    <p style="margin:0; font-size:14px; line-height:1.8;">
                        <strong>Subtotal:</strong> {Moeda(pedido.Subtotal)}<br />
                        {(pedido.Desconto > 0 ? $"<strong>Desconto{(pedido.CupomCodigo is null ? "" : $" ({Escapar(pedido.CupomCodigo)})")}:</strong> {Moeda(pedido.Desconto)}<br />" : string.Empty)}
                        <strong>Frete:</strong> {Moeda(pedido.CustoFrete)}<br />
                        <strong>Total:</strong> {Moeda(pedido.Total)}
                    </p>
                </div>
                {enderecoHtml}
                {(aguardando ? "<p style=\"margin-top:16px; color:#666666; font-size:14px;\">Assim que combinarmos a forma de pagamento, te avisamos por aqui.</p>" : string.Empty)}
                <p style="margin-top:16px; color:#666666; font-size:13px;">Atenciosamente, equipe Lumi Makeup</p>
            </div>
            """;
    }

    private static string RelatorioDosItens(PedidoDto pedido)
    {
        var itens = new StringBuilder();

        foreach (var item in pedido.Itens)
        {
            itens.AppendLine($"• {item.Quantidade}x {item.Nome} — {Moeda(item.Subtotal)}");
        }

        return itens.ToString().Trim();
    }

    private static string PrimeiroNome(string nome) =>
        nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nome;

    private static string Moeda(decimal valor) =>
        valor.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));

    /// <summary>
    /// O nome do produto e o nome do cliente vêm de quem os digitou, e entram
    /// direto no HTML. Sem escapar, um produto chamado <b> dia </b> quebraria o
    /// e-mail, e o mais grave: um nome com tag pareceria conteúdo em vez de texto.
    /// </summary>
    private static string Escapar(string valor) => WebUtility.HtmlEncode(valor);
}
