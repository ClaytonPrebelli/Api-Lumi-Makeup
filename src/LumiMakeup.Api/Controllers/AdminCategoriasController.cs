using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. O servidor de producao so
 * encaminha GET, POST, HEAD, OPTIONS e TRACE; PUT e DELETE sao recusados pelo IIS
 * antes de chegar aqui. Por isso atualizar e excluir sao POST com o verbo no fim
 * da URL. Ver a explicacao completa em AdminProdutosController.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/categorias")]
public sealed class AdminCategoriasController : ControllerBase
{
    private readonly IGestaoDeCategoriasService _gestaoDeCategorias;

    public AdminCategoriasController(IGestaoDeCategoriasService gestaoDeCategorias)
    {
        _gestaoDeCategorias = gestaoDeCategorias;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodas(CancellationToken cancellationToken)
    {
        var categorias = await _gestaoDeCategorias.ObterTodasAsync(cancellationToken);
        return Ok(categorias);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeCategoria requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var categoria = await _gestaoDeCategorias.CriarAsync(requisicao, cancellationToken);
            return CreatedAtAction(nameof(ObterTodas), categoria);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/atualizar")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] RequisicaoDeCategoria requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var categoria = await _gestaoDeCategorias.AtualizarAsync(id, requisicao, cancellationToken);
            return Ok(categoria);
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
            await _gestaoDeCategorias.ExcluirAsync(id, cancellationToken);
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
