using System.Security.Claims;
using LumiMakeup.Aplicacao.Abstracoes;
using LumiMakeup.Aplicacao.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/autenticacao")]
public sealed class AutenticacaoController : ControllerBase
{
    private readonly IServicoDeAutenticacao _servicoDeAutenticacao;

    public AutenticacaoController(IServicoDeAutenticacao servicoDeAutenticacao)
    {
        _servicoDeAutenticacao = servicoDeAutenticacao;
    }

    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] RequisicaoDeRegistro requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _servicoDeAutenticacao.CadastrarAsync(requisicao, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("entrar")]
    public async Task<IActionResult> Entrar([FromBody] RequisicaoDeLogin requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _servicoDeAutenticacao.EntrarAsync(requisicao, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("google")]
    public async Task<IActionResult> EntrarComGoogle([FromBody] RequisicaoDeLoginGoogle requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _servicoDeAutenticacao.EntrarComGoogleAsync(requisicao, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("renovar")]
    public async Task<IActionResult> Renovar([FromBody] RequisicaoDeRenovacao requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _servicoDeAutenticacao.RenovarAsync(requisicao.TokenRefresh, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpGet("eu")]
    public async Task<IActionResult> ObterUsuarioAtual(CancellationToken cancellationToken)
    {
        var usuarioId = ObterIdDoUsuario();
        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var usuario = await _servicoDeAutenticacao.ObterUsuarioAtualAsync(usuarioId.Value, cancellationToken);
        return Ok(usuario);
    }

    [Authorize]
    [HttpPost("completar-perfil")]
    public async Task<IActionResult> CompletarPerfil([FromBody] RequisicaoDeCompletarPerfil requisicao, CancellationToken cancellationToken)
    {
        var usuarioId = ObterIdDoUsuario();
        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            var usuario = await _servicoDeAutenticacao.CompletarPerfilAsync(usuarioId.Value, requisicao, cancellationToken);
            return Ok(usuario);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private long? ObterIdDoUsuario()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : null;
    }
}