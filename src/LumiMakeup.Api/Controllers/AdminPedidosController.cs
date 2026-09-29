using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. O servidor de producao so
 * encaminha GET, POST, HEAD, OPTIONS e TRACE; PUT e DELETE sao recusados pelo IIS
 * antes de chegar aqui. Por isso registrar pagamento e cancelar sao POST com o
 * verbo no fim da URL.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/pedidos")]
public sealed class AdminPedidosController : ControllerBase
{
    private readonly IGestaoDePedidosService _pedidos;

    public AdminPedidosController(IGestaoDePedidosService pedidos)
    {
        _pedidos = pedidos;
    }

    /// <summary>
    /// Listagem do painel. Todos os filtros são opcionais, e `semNotaFiscal` é o
    /// que responde "o que ainda falta emitir".
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] StatusPedido? status,
        [FromQuery] OrigemPedido? origem,
        [FromQuery] bool? semNotaFiscal,
        CancellationToken cancellationToken)
    {
        var pedidos = await _pedidos.ListarAsync(
            status,
            origem,
            // A query string diz "sem nota fiscal" porque é assim que a tela
            // pergunta; o serviço recebe o valor da flag. A negação acontece uma
            // vez, aqui, com nome explícito dos dois lados.
            notaFiscalGerada: semNotaFiscal is null ? null : !semNotaFiscal.Value,
            cancellationToken);

        return Ok(pedidos);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObterPorId(long id, CancellationToken cancellationToken)
    {
        var pedido = await _pedidos.ObterPorIdAsync(id, cancellationToken);

        if (pedido is null)
        {
            return NotFound(new { message = "Pedido não encontrado." });
        }

        return Ok(pedido);
    }

    /// <summary>
    /// Registra a venda de balcão.
    ///
    /// Vai para a mesma rota de criação do checkout, mudando a origem: a venda
    /// presencial baixa estoque, consome cupom e entra no relatório exatamente
    /// como a de online. Passando por um caminho próprio, uma das duas formas
    /// acabaria esquecendo de baixar o estoque ou de avisar o cliente.
    ///
    /// O cliente é o do token. A administradora cadastra a pessoa primeiro, como
    /// quem registra um cliente novo, e só então registra a venda — sem conta não
    /// há telefone, e o telefone é por onde o WhatsApp chega.
    /// </summary>
    [HttpPost("balcao")]
    public async Task<IActionResult> CriarVendaDeBalcao(
        [FromBody] RequisicaoDeVendaDeBalcao requisicao,
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
                    null,
                    requisicao.CupomCodigo,
                    requisicao.Observacoes,
                    0m,
                    0m,
                    requisicao.CriadoEm),
                OrigemPedido.Balcao,
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

    /// <summary>
    /// A administradora anota a forma de pagamento e dá o aceite. É o "finalizar"
    /// do fluxo: o pedido sai de <c>AguardandoPagamento</c> para <c>Pago</c>.
    ///
    /// Não há verificação de compensação bancária — quem confirma o recebimento é
    /// a administradora.
    /// </summary>
    [HttpPost("{id:long}/pagamento")]
    public async Task<IActionResult> RegistrarPagamento(
        long id,
        [FromBody] RequisicaoDePagamentoDePedido requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var pedido = await _pedidos.RegistrarPagamentoAsync(id, requisicao.MetodoPagamento, cancellationToken);
            return Ok(pedido);
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

    /// <summary>Cancelar devolve o estoque e a unidade de cupom.</summary>
    [HttpPost("{id:long}/cancelar")]
    public async Task<IActionResult> Cancelar(long id, CancellationToken cancellationToken)
    {
        try
        {
            var pedido = await _pedidos.CancelarAsync(id, cancellationToken);
            return Ok(pedido);
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
