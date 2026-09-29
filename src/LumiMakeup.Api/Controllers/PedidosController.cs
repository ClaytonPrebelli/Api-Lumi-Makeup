using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pedidos")]
public sealed class PedidosController : ControllerBase
{
    private readonly IGestaoDePedidosService _pedidos;

    public PedidosController(IGestaoDePedidosService pedidos)
    {
        _pedidos = pedidos;
    }

    /// <summary>
    /// Finaliza a compra do checkout.
    ///
    /// O cliente vem do token e o pedido nasce em <c>AguardandoPagamento</c>, sem
    /// forma de pagamento: quem anota é a administradora, depois de falar com o
    /// cliente. Por isso a rota não aceita pagamento nenhum.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeCriacaoDePedido requisicao,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            var pedido = await _pedidos.CriarAsync(
                new RequisicaoDePedido(
                    usuarioId.Value,
                    requisicao.Itens,
                    requisicao.Endereco,
                    requisicao.CupomCodigo,
                    requisicao.Observacoes,
                    requisicao.CustoFrete,
                    requisicao.DistanciaKm,
                    null),
                OrigemPedido.Online,
                cancellationToken);

            return CreatedAtAction(nameof(ObterPorId), new { id = pedido.Id }, pedido);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var pedidos = await _pedidos.ListarDoUsuarioAsync(usuarioId.Value, cancellationToken);
        return Ok(pedidos);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObterPorId(long id, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var pedido = await _pedidos.ObterPorIdAsync(id, cancellationToken);

        // Pedido de outra pessoa responde 404, e não 403: confirmar que o pedido
        // existe já entrega informação de outro cliente.
        if (pedido is null || pedido.UsuarioId != usuarioId.Value)
        {
            return NotFound(new { message = "Pedido não encontrado." });
        }

        return Ok(pedido);
    }
}
