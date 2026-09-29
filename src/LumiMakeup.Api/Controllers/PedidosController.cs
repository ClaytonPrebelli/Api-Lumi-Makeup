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
    /// forma de pagamento: quem anota é a administradora, depois de falar com o
    /// cliente. Por isso a rota não aceita pagamento nenhum.
    /// </summary>
    /// <summary>
    /// Calcula o frete de um endereço, para o checkout mostrar antes de confirmar.
    ///
    /// O valor devolvido aqui é **prévia**. O cobrado é recalculado em
    /// <see cref="Criar"/>, sobre o endereço que o próprio servidor gravou. Confiar
    /// no número que o navegador mandaria seria deixar o cliente escolher o
    /// próprio frete.
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
            // O frete é calculado aqui, com o endereço que o corpo traz, e não
            // lido do corpo como `CustoFrete`. A razão é a mesma do cupom: o
            // número que o navegador manda não é conferido, e frete inventado pelo
            // cliente é frete grátis.
            var endereco = requisicao.Endereco
                ?? throw new InvalidOperationException("Escolha o endereço de entrega.");

            var calculo = await _frete.CalcularAsync(endereco, cancellationToken);

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
