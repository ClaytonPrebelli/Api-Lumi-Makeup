using System.Security.Claims;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/autenticacao")]
public sealed class AutenticacaoController : ControllerBase
{
    private readonly IAutenticacaoService _autenticacaoService;
    private readonly IRecuperacaoDeSenhaService _recuperacaoDeSenhaService;

    public AutenticacaoController(IAutenticacaoService autenticacaoService, IRecuperacaoDeSenhaService recuperacaoDeSenhaService)
    {
        _autenticacaoService = autenticacaoService;
        _recuperacaoDeSenhaService = recuperacaoDeSenhaService;
    }

    [HttpPost("cadastrar")]
    public async Task<IActionResult> Cadastrar([FromBody] RequisicaoDeRegistro requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _autenticacaoService.CadastrarAsync(requisicao, cancellationToken);
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
            var response = await _autenticacaoService.EntrarAsync(requisicao, cancellationToken);
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
            var response = await _autenticacaoService.EntrarComGoogleAsync(requisicao, cancellationToken);
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
            var response = await _autenticacaoService.RenovarAsync(requisicao.TokenRefresh, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("solicitar-reset-senha")]
    public async Task<IActionResult> SolicitarResetDeSenha([FromBody] RequisicaoDeSolicitarResetDeSenha requisicao, CancellationToken cancellationToken)
    {
        await _recuperacaoDeSenhaService.SolicitarAsync(requisicao, cancellationToken);

        return Ok(new { mensagem = "Se o e-mail informado existir, você receberá as instruções para redefinir a senha." });
    }

    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha([FromBody] RequisicaoDeConfirmarResetDeSenha requisicao, CancellationToken cancellationToken)
    {
        try
        {
            var resposta = await _recuperacaoDeSenhaService.ConfirmarAsync(requisicao, cancellationToken);
            return Ok(resposta);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
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

        var usuario = await _autenticacaoService.ObterUsuarioAtualAsync(usuarioId.Value, cancellationToken);
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
            var usuario = await _autenticacaoService.CompletarPerfilAsync(usuarioId.Value, requisicao, cancellationToken);
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