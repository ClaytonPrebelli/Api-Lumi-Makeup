using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra dos outros controllers do painel. O servidor de producao
 * so encaminha GET, POST, HEAD, OPTIONS e TRACE; atualizar e POST com o verbo
 * no fim da URL.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/entregadores")]
public sealed class AdminEntregadoresController : ControllerBase
{
    private readonly IGestaoDeEntregadoresService _entregadores;

    public AdminEntregadoresController(IGestaoDeEntregadoresService entregadores)
    {
        _entregadores = entregadores;
    }

    /// <summary>Entregadores cadastrados, por nome.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        return Ok(await _entregadores.ListarAsync(cancellationToken));
    }

    /// <summary>
    /// Cadastra o entregador com usuário e senha. Sem e-mail: ele entra no
    /// portal de entrega com esse usuário.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeEntregador requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var entregador = await _entregadores.CriarAsync(requisicao, cancellationToken);
            return CreatedAtAction(nameof(Listar), entregador);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Atualiza nome, usuário e ativo. Senha em branco mantém a atual.
    /// Desativar tira o acesso sem apagar o histórico de entregas.
    /// </summary>
    [HttpPost("{id:long}/atualizar")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] RequisicaoDeAtualizacaoDeEntregador requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _entregadores.AtualizarAsync(id, requisicao, cancellationToken));
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
