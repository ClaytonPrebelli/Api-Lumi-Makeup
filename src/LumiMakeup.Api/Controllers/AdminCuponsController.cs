using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. O servidor de producao so
 * encaminha GET, POST, HEAD, OPTIONS e TRACE; PUT e DELETE sao recusados pelo IIS
 * antes de chegar aqui. Por isso ativar e somar quantidade sao POST com o verbo no
 * fim da URL.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/cupons")]
public sealed class AdminCuponsController : ControllerBase
{
    private readonly IGestaoDeCuponsService _cupons;

    public AdminCuponsController(IGestaoDeCuponsService cupons)
    {
        _cupons = cupons;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
    {
        var cupons = await _cupons.ObterTodosAsync(cancellationToken);
        return Ok(cupons);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeCupom requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var cupom = await _cupons.CriarAsync(requisicao, cancellationToken);
            return CreatedAtAction(nameof(ObterTodos), cupom);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/atualizar")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] RequisicaoDeCupom requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var cupom = await _cupons.AtualizarAsync(id, requisicao, cancellationToken);
            return Ok(cupom);
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

    [HttpPost("{id:long}/ativo")]
    public async Task<IActionResult> DefinirAtivo(
        long id,
        [FromBody] RequisicaoDeAtivacaoDeCupom requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var cupom = await _cupons.DefinirAtivoAsync(id, requisicao.Ativo, cancellationToken);
            return Ok(cupom);
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
    /// Soma unidades ao estoque de usos. Rota separada da edição porque a
    /// quantidade na tela é um **incremento**, e não o valor absoluto: reescrever o
    /// valor absoluto com duas pessoas no painel apagaria o uso da outra.
    /// </summary>
    [HttpPost("{id:long}/quantidade")]
    public async Task<IActionResult> SomarQuantidade(
        long id,
        [FromBody] RequisicaoDeSomaDeQuantidadeDeCupom requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var cupom = await _cupons.SomarQuantidadeAsync(id, requisicao.Quantidade, cancellationToken);
            return Ok(cupom);
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

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _cupons.ExcluirAsync(id, cancellationToken);
            return NoContent();
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
