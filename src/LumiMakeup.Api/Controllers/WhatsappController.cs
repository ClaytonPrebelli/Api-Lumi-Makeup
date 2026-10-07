using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/// <summary>
/// Status do WhatsApp para a loja (cliente logado).
///
/// É a mesma leitura do painel, sem nada de administração: o front chama ao
/// abrir o carrinho e ao adicionar item, e o tráfego de entrada acorda o Node
/// no Render. Assim, na hora de finalizar o pedido, a confirmação por
/// WhatsApp já encontra o serviço de pé. Nunca lança: Node parado devolve
/// o status desligado, e o front ignora o resultado.
/// </summary>
[ApiController]
[Authorize]
[Route("api/whatsapp")]
public sealed class WhatsappController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;

    public WhatsappController(IWhatsAppService whatsApp)
    {
        _whatsApp = whatsApp;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        return Ok(await _whatsApp.ObterStatusAsync(cancellationToken));
    }
}
