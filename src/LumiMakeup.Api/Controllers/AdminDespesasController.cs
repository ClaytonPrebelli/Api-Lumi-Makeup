using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/despesas")]
public sealed class AdminDespesasController : ControllerBase
{
    private readonly IGestaoDeDespesasService _despesas;

    public AdminDespesasController(IGestaoDeDespesasService despesas)
    {
        _despesas = despesas;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateTime? inicio,
        [FromQuery] DateTime? fim,
        [FromQuery] string? categoria,
        CancellationToken cancellationToken)
    {
        var despesas = await _despesas.ListarAsync(inicio, fim, categoria, cancellationToken);
        return Ok(despesas);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeDespesa requisicao,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            var despesa = await _despesas.CriarAsync(requisicao, usuarioId.Value, cancellationToken);
            return CreatedAtAction(nameof(Listar), despesa);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/atualizar")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] RequisicaoDeAtualizacaoDeDespesa requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var despesa = await _despesas.AtualizarAsync(id, requisicao, cancellationToken);
            return Ok(despesa);
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
            await _despesas.ExcluirAsync(id, cancellationToken);
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