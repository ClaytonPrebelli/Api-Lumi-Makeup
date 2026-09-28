using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

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

    [HttpPut("{id:long}")]
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

    [HttpDelete("{id:long}")]
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
