using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * Portal do entregador (e admin). VERBOS: mesma regra do painel — o servidor
 * de producao so encaminha GET, POST, HEAD, OPTIONS e TRACE.
 */
[ApiController]
[Authorize(Policy = "Entrega")]
[Route("api/entregas")]
public sealed class EntregasController : ControllerBase
{
    private readonly IGestaoDePedidosService _pedidos;

    public EntregasController(IGestaoDePedidosService pedidos)
    {
        _pedidos = pedidos;
    }

    /// <summary>
    /// Pedidos despachados (todos) e entregues para o portal e os relatórios.
    ///
    /// Entregue filtra por quem entregou, a menos que quem pergunte seja
    /// admin — que pode ver tudo ou filtrar por entregador. O intervalo vale
    /// para a data do estágio filtrado.
    /// </summary>
    [HttpGet("pedidos")]
    public async Task<IActionResult> Listar(
        [FromQuery] StatusEntrega? statusEntrega,
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] long? entregadorId,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var ehAdmin = User.IsInRole("Administrador");

        var pedidos = await _pedidos.ListarEntregasAsync(
            statusEntrega,
            de,
            ate,
            ehAdmin ? entregadorId : usuarioId.Value,
            cancellationToken);

        return Ok(pedidos);
    }

    /// <summary>
    /// Marca o pedido como entregue, gravando quem entregou e quando. Só sai
    /// de despachado para entregue. Vale para entregador e admin.
    /// </summary>
    [HttpPost("pedidos/{id:long}/entregar")]
    public async Task<IActionResult> RegistrarEntrega(long id, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _pedidos.RegistrarEntregaAsync(id, usuarioId.Value, cancellationToken));
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
}
