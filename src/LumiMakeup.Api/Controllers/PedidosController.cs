using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pedidos")]
public sealed class PedidosController : ControllerBase
{
    private readonly IGestaoDePedidosService _pedidos;
    private readonly ICalculoDeFreteService _frete;

    public PedidosController(IGestaoDePedidosService pedidos, ICalculoDeFreteService frete)
    {
        _pedidos = pedidos;
        _frete = frete;
    }

    /// <summary>
    /// Finaliza a compra do checkout.
    ///
    /// O cliente vem do token e o pedido nasce em <c>AguardandoPagamento</c>, sem
    /// forma de pagamento: quem anota Ã© a administradora, depois de falar com o
    /// cliente. Por isso a rota nÃ£o aceita pagamento nenhum.
    /// </summary>
    /// <summary>
    /// Calcula o frete de um endereÃ§o, para o checkout mostrar antes de confirmar.
    ///
    /// O valor devolvido aqui Ã© **prÃ©via**. O cobrado Ã© recalculado em
    /// <see cref="Criar"/>, sobre o endereÃ§o que o prÃ³prio servidor gravou. Confiar
    /// no nÃºmero que o navegador mandaria seria deixar o cliente escolher o
    /// prÃ³prio frete.
    /// </summary>
    [HttpPost("frete")]
    public async Task<IActionResult> CalcularFrete(
        [FromBody] EnderecoDeEntregaRequisicao endereco,
        CancellationToken cancellationToken)
    {
        try
        {
            var calculo = await _frete.CalcularAsync(endereco, cancellationToken);
            return Ok(calculo);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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
            var endereco = requisicao.Endereco;

            if (!requisicao.Retirada && endereco is null)
            {
                throw new InvalidOperationException("Escolha o endereço de entrega.");
            }

            var calculo = requisicao.Retirada
                ? new CalculoDeFreteDto(0m, 0m, 0m, 0m)
                : await _frete.CalcularAsync(endereco!, cancellationToken);

            var pedido = await _pedidos.CriarAsync(
                new RequisicaoDePedido(
                    usuarioId.Value,
                    requisicao.Itens,
                    endereco,
                    requisicao.CupomCodigo,
                    requisicao.Observacoes,
                    calculo.Custo,
                    calculo.DistanciaKm,
                    null),
                requisicao.Retirada ? OrigemPedido.Balcao : OrigemPedido.Online,
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
        catch (IOException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return StatusCode(500, new { message = $"{ex.GetType().Name}: {ex.Message}" });
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            return StatusCode(500, new { message = $"{ex.GetType().Name}: {ex.Message}" });
        }
        catch (DbUpdateException ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            var detalhe = ex.InnerException?.Message ?? ex.Message;
            return StatusCode(500, new { message = $"Falha ao gravar o pedido: {detalhe}" });
        }
        catch (Exception ex)
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            var inner = ex.InnerException?.ToString() ?? "";
            return StatusCode(500, new { message = $"{ex.GetType().Name}: {ex.Message}. Inner: {inner}" });
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

        // Pedido de outra pessoa responde 404, e nÃ£o 403: confirmar que o pedido
        // existe jÃ¡ entrega informaÃ§Ã£o de outro cliente.
        if (pedido is null || pedido.UsuarioId != usuarioId.Value)
        {
            return NotFound(new { message = "Pedido nÃ£o encontrado." });
        }

        return Ok(pedido);
    }
}
