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
    private readonly IGestaoDeWhatsAppService _gestaoWhatsApp;
    private readonly NotificacoesDePedidoOptions _opcoes;
    private readonly ILogger<NotificadorDePedido> _logger;

    public NotificadorDePedido(
        IEmailSender email,
        IWhatsAppService whatsApp,
        IGestaoDeWhatsAppService gestaoWhatsApp,
        IOptions<NotificacoesDePedidoOptions> opcoes,
        ILogger<NotificadorDePedido> logger)
    {
        _email = email;
        _whatsApp = whatsApp;
        _gestaoWhatsApp = gestaoWhatsApp;
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

            var enviada = await _whatsApp.EnviarMensagemAsync(
                numero,
                await MensagemDeWhatsAppAsync(pedido, cancellationToken),
                cancellationToken);

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
    private async Task<string> MensagemDeWhatsAppAsync(
        PedidoDto pedido,
        CancellationToken cancellationToken)
    {
        var configuracao = await _gestaoWhatsApp.ObterAsync(cancellationToken);

        return MontadorDeMensagemDePedido.Montar(
            configuracao.MensagemInicialCliente,
            pedido,
            _opcoes.NomeDaLoja);
    }

    private static string CorpoDoPedidoParaAdministradora(PedidoDto pedido)
    {
        var origem = pedido.Origem is Domain.Enums.OrigemPedido.Balcao
            ? "Venda de balcão"
            : "Pedido online";

        var linkParaPedidosAdmin = "https://lumimakeup.com.br/minha-conta/admin/pedidos";

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
                <!DOCTYPE html>
                <html lang="pt-BR">
                <body style="margin:0;padding:0;background-color:#f3e4da;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f3e4da;padding:32px 12px;">
                    <tr>
                      <td align="center">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:10px;box-shadow:0 4px 14px rgba(74,51,44,0.08);overflow:hidden;">
                          <tr>
                            <td style="height:6px;background-color:#b98b73;"></td>
                          </tr>
                          <tr>
                            <td style="padding:40px 44px 32px 44px;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:28px;letter-spacing:3px;color:#8b5e52;font-weight:600;">LUMI&nbsp;MAKEUP</div>
                              <div style="font-family:'Brush Script MT','Segoe Print',cursive;font-size:20px;color:#b98b73;margin-top:2px;">Seu brilho começa aqui</div>
                              <div style="color:#b98b73;font-size:14px;margin:18px 0 22px 0;">&#10084;&nbsp;&nbsp;&#10084;&nbsp;&nbsp;&#10084;</div>
                              <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:26px;color:#4a332c;margin:0 0 10px 0;font-weight:600;">Novo pedido recebido</h1>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:15px;line-height:1.6;color:#4a332c;margin:0 0 20px 0;text-align:left;">
                                Um {origem.ToLowerInvariant()} foi registrado e está aguardando pagamento.<br />
                                <strong>Pedido:</strong> {pedido.Id}<br />
                                <strong>Cliente:</strong> {Escapar(pedido.NomeCliente)}<br />
                                <strong>Telefone:</strong> {Escapar(pedido.TelefoneContato ?? "não informado")}<br />
                                <strong>E-mail:</strong> {Escapar(pedido.EmailContato ?? "não informado")}<br />
                                <strong>Recebido em:</strong> {pedido.CriadoEm.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"))}
                              </p>
                              <div style="text-align:left;">
                                {itens}
                                <div style="margin-top:16px; padding:16px; background:#f8f3ef; border-radius:8px; border:1px solid #e0e0e0;">
                                  <p style="margin:0; font-size:14px; line-height:1.8; color:#4a332c;">
                                    <strong>Subtotal:</strong> {Moeda(pedido.Subtotal)}<br />
                                    {(pedido.Desconto > 0 ? $"<strong>Desconto{(pedido.CupomCodigo is null ? "" : $" ({Escapar(pedido.CupomCodigo)})")}:</strong> {Moeda(pedido.Desconto)}<br />" : string.Empty)}
                                    <strong>Frete:</strong> {Moeda(pedido.CustoFrete)}<br />
                                    <strong>Total:</strong> {Moeda(pedido.Total)}
                                  </p>
                                </div>
                                {enderecoHtml}
                              </div>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:14px;line-height:1.6;color:#8a7268;margin:18px 0 0 0;">
                                <a href="{linkParaPedidosAdmin}" style="color:#8b5e52;">Ver pedido no painel</a>
                              </p>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:13px;line-height:1.6;color:#8a7268;margin:12px 0 0 0;">A nota fiscal deste pedido ainda não foi gerada.</p>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:26px 44px;border-top:1px solid #e7d6ca;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:16px;color:#8b5e52;">Equipe Lumi Makeup</div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
                """;
    }

    private static string CorpoDoPedido(PedidoDto pedido, string assunto)
    {
        var linkParaPedidos = "https://lumimakeup.com.br/minha-conta/pedidos";

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
                <!DOCTYPE html>
                <html lang="pt-BR">
                <body style="margin:0;padding:0;background-color:#f3e4da;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f3e4da;padding:32px 12px;">
                    <tr>
                      <td align="center">
                        <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="width:100%;max-width:600px;background-color:#ffffff;border-radius:10px;box-shadow:0 4px 14px rgba(74,51,44,0.08);overflow:hidden;">
                          <tr>
                            <td style="height:6px;background-color:#b98b73;"></td>
                          </tr>
                          <tr>
                            <td style="padding:40px 44px 32px 44px;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:28px;letter-spacing:3px;color:#8b5e52;font-weight:600;">LUMI&nbsp;MAKEUP</div>
                              <div style="font-family:'Brush Script MT','Segoe Print',cursive;font-size:20px;color:#b98b73;margin-top:2px;">Seu brilho começa aqui</div>
                              <div style="color:#b98b73;font-size:14px;margin:18px 0 22px 0;">&#10084;&nbsp;&nbsp;&#10084;&nbsp;&nbsp;&#10084;</div>
                              <h1 style="font-family:Georgia,'Playfair Display',serif;font-size:26px;color:#4a332c;margin:0 0 10px 0;font-weight:600;">{Escapar(assunto)}</h1>
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:15px;line-height:1.6;color:#4a332c;margin:0 0 20px 0;">
                                Olá, <strong>{Escapar(PrimeiroNome(pedido.NomeCliente))}</strong>! O número do seu pedido é <strong>{pedido.Id}</strong>.
                              </p>
                              <div style="text-align:left;">
                                {itens}
                                <div style="margin-top:16px; padding:16px; background:#f8f3ef; border-radius:8px; border:1px solid #e0e0e0;">
                                  <p style="margin:0; font-size:14px; line-height:1.8; color:#4a332c;">
                                    <strong>Subtotal:</strong> {Moeda(pedido.Subtotal)}<br />
                                    {(pedido.Desconto > 0 ? $"<strong>Desconto{(pedido.CupomCodigo is null ? "" : $" ({Escapar(pedido.CupomCodigo)})")}:</strong> {Moeda(pedido.Desconto)}<br />" : string.Empty)}
                                    <strong>Frete:</strong> {Moeda(pedido.CustoFrete)}<br />
                                    <strong>Total:</strong> {Moeda(pedido.Total)}
                                  </p>
                                </div>
                                {enderecoHtml}
                              </div>
                              {(aguardando ? "<p style=\"font-family:Arial,'Inter',sans-serif;font-size:14px;line-height:1.6;color:#8a7268;margin:18px 0 0 0;\">Assim que combinarmos a forma de pagamento, te avisamos por aqui.</p>" : string.Empty)}
                              <p style="font-family:Arial,'Inter',sans-serif;font-size:14px;line-height:1.6;color:#8a7268;margin:18px 0 0 0;">
                                <a href="{linkParaPedidos}" style="color:#8b5e52;">Ver meus pedidos</a>
                              </p>
                            </td>
                          </tr>
                          <tr>
                            <td style="padding:26px 44px;border-top:1px solid #e7d6ca;text-align:center;">
                              <div style="font-family:Georgia,'Playfair Display',serif;font-size:16px;color:#8b5e52;">Equipe Lumi Makeup</div>
                              <div style="font-family:Arial,'Inter',sans-serif;font-size:12px;color:#8a7268;margin-top:6px;">Atenciosamente, equipe Lumi Makeup</div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>
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
