using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/recompra")]
public sealed class AdminRecompraController : ControllerBase
{
    private readonly IRecompraService _recompra;

    public AdminRecompraController(IRecompraService recompra)
    {
        _recompra = recompra;
    }

    [HttpGet]
    public async Task<IActionResult> ObterConfiguracao(CancellationToken cancellationToken)
    {
        var configuracao = await _recompra.ObterConfiguracaoAsync(cancellationToken);
        return Ok(configuracao);
    }

    [HttpPost]
    public async Task<IActionResult> SalvarConfiguracao(
        [FromBody] RequisicaoDeConfiguracaoDeRecompra requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _recompra.SalvarConfiguracaoAsync(requisicao, cancellationToken));
        }
        catch (InvalidOperationException excecao)
        {
            return BadRequest(new { message = excecao.Message });
        }
    }

    [HttpPost("disparar")]
    public async Task<IActionResult> Disparar(CancellationToken cancellationToken)
    {
        var resultado = await _recompra.DispararAsync(cancellationToken);
        return Ok(resultado);
    }
}
