using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/// <summary>
/// Busca e cadastro de cliente para a venda de balcão.
///
/// A administradora precisa achar a pessoa — para uma cliente que já compra, é
/// buscar pelo nome ou telefone; para a primeira vez, é cadastrar sem senha.
/// Nenhuma das duas é autenticação, e por isso fica aqui em vez de em
/// <c>AutenticacaoController</c>.
/// </summary>
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/clientes")]
public sealed class AdminClientesController : ControllerBase
{
    private readonly IGestaoDeClientesService _clientes;

    public AdminClientesController(IGestaoDeClientesService clientes)
    {
        _clientes = clientes;
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? termo,
        CancellationToken cancellationToken)
    {
        var clientes = await _clientes.BuscarAsync(termo, cancellationToken);
        return Ok(clientes);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeCliente requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var cliente = await _clientes.CriarAsync(requisicao, cancellationToken);
            return CreatedAtAction(nameof(Buscar), cliente);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
