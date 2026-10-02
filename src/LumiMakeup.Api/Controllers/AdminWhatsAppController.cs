using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminFreteController. Todas as acoes sao POST, porque
 * o servidor de producao nao encaminha PUT nem PATCH.
 *
 * O QR do numero da loja so passa por aqui. A rota /pareamento do Node nao e
 * exposta na internet: quem pede o QR e a API, com o segredo, e o navegador
 * recebe pela API. Expor o Node significaria que qualquer pagina aberta por
 * baixo consumiria o QR do acesso a conta.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/whatsapp")]
public sealed class AdminWhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;
    private readonly IGestaoDeWhatsAppService _gestaoWhatsApp;
    private readonly ILogger<AdminWhatsAppController> _logger;

    public AdminWhatsAppController(
        IWhatsAppService whatsApp,
        IGestaoDeWhatsAppService gestaoWhatsApp,
        ILogger<AdminWhatsAppController> logger)
    {
        _whatsApp = whatsApp;
        _gestaoWhatsApp = gestaoWhatsApp;
        _logger = logger;
    }

    /// <summary>
    /// Se o Node esta no ar e se o numero esta pareado.
    ///
    /// Nunca lanca: Node parado e um estado legitimo da tela, nao erro 500. A
    /// administradora precisa ver "o servico esta desligado" e saber o que
    /// fazer, e nao uma tela branca de excecao.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await _whatsApp.ObterStatusAsync(cancellationToken);

        return Ok(status);
    }

    /// <summary>
    /// O QR do pareamento, em PNG base64. Vazio quando ainda nao ha QR.
    /// </summary>
    [HttpPost("pareamento")]
    public async Task<IActionResult> Pareamento(CancellationToken cancellationToken)
    {
        var qr = await _whatsApp.ObterQrDePareamentoAsync(cancellationToken);

        if (qr is null)
        {
            _logger.LogInformation(
                "Pareamento pedido e nao ha QR disponivel. Ou o numero ja esta pareado, ou o WhatsApp bloqueou.");

            return Ok(new { pareado = false, qr = (string?)null });
        }

        return Ok(new { pareado = false, qr });
    }

    /// <summary>
    /// Forca uma nova tentativa de conexao, e e o que o botao "Gerar novo QR"
    /// chama.
    ///
    /// Sem esta rota o botao nao faria nada: o Node so releria o QR atual, e
    /// entre uma tentativa e outra nao existe socket, entao nao existe QR para
    /// reler. A tela pareceria quebrada.
    /// </summary>
    [HttpPost("pareamento/reconectar")]
    public async Task<IActionResult> Reconectar(CancellationToken cancellationToken)
    {
        var qr = await _whatsApp.ForcarReconexaoDePareamentoAsync(cancellationToken);

        return Ok(new
        {
            pareado = false,
            qr,
            aguardandoSegundos = 0
        });
    }

    [HttpGet("mensagem")]
    public async Task<IActionResult> ObterMensagem(CancellationToken cancellationToken)
    {
        var configuracao = await _gestaoWhatsApp.ObterAsync(cancellationToken);
        return Ok(configuracao);
    }

    [HttpPost("mensagem")]
    public async Task<IActionResult> SalvarMensagem(
        [FromBody] RequisicaoDeConfiguracaoWhatsApp requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuracao = await _gestaoWhatsApp.SalvarAsync(requisicao, cancellationToken);
            return Ok(configuracao);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("mensagem/previa")]
    public async Task<IActionResult> PreviaMensagem(CancellationToken cancellationToken)
    {
        var previa = await _gestaoWhatsApp.GerarPreviaAsync(cancellationToken);
        return Ok(previa);
    }
}
