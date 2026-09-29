using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/// <summary>
/// Conferência de cupom para o checkout.
///
/// O desconto devolvido aqui é **prévia**. O valor cobrado vem do
/// <c>GestaoDePedidosService</c>, que recalcula sobre o subtotal que ele próprio
/// montou. Confiar no número que o navegador mandou seria deixar o cliente
/// escolher o próprio desconto.
///
/// Por isso o subtotal vem do corpo: é só para a prévia, e não entra em conta
/// nenhuma na gravação.
/// </summary>
[ApiController]
[Route("api/cupons")]
public sealed class CuponsController : ControllerBase
{
    private readonly IGestaoDeCuponsService _cupons;

    public CuponsController(IGestaoDeCuponsService cupons)
    {
        _cupons = cupons;
    }

    [HttpPost("validar")]
    public async Task<IActionResult> Validar(
        [FromBody] RequisicaoDeValidacaoDeCupom requisicao,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requisicao.Codigo))
        {
            return BadRequest(new { message = "Informe o código do cupom." });
        }

        if (requisicao.SubtotalDosProdutos < 0)
        {
            return BadRequest(new { message = "O subtotal não pode ser negativo." });
        }

        try
        {
            var aplicacao = await _cupons.CalcularAsync(
                requisicao.Codigo,
                requisicao.SubtotalDosProdutos,
                DateTime.UtcNow,
                cancellationToken);

            return Ok(aplicacao);
        }
        catch (InvalidOperationException ex)
        {
            // A mensagem é a que a pessoa vai ler: "cupom inválido" para
            // desativado, esgotado e inexistente; "cupom expirado" para vencido; e
            // o valor mínimo quando o subtotal não chega nele.
            return BadRequest(new { message = ex.Message });
        }
    }
}
