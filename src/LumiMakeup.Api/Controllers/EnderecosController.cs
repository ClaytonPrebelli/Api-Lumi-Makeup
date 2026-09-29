using LumiMakeup.Api.Extensions;
using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/enderecos")]
public sealed class EnderecosController : ControllerBase
{
    private readonly IGestaoDeEnderecosService _enderecos;

    public EnderecosController(IGestaoDeEnderecosService enderecos)
    {
        _enderecos = enderecos;
    }

    /// <summary>Agenda do cliente do token. O padrão vem primeiro.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var enderecos = await _enderecos.ListarDoUsuarioAsync(usuarioId.Value, cancellationToken);
        return Ok(enderecos);
    }

    /// <summary>
    /// Cadastra um endereço na agenda. É o que a tela de checkout chama quando a
    /// pessoa usa um endereço novo: ela não é obrigada a digitar o mesmo lugar na
    /// próxima compra.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeCriacaoDeEndereco requisicao,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            var endereco = await _enderecos.CriarAsync(usuarioId.Value, requisicao.Endereco, cancellationToken);
            return CreatedAtAction(nameof(Listar), endereco);
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
    /// Corrige o endereço de um pedido que ainda não saiu para entrega.
    ///
    /// Rota separada do POST de criação porque é a única forma de mexer no
    /// endereço de um pedido já gravado — e ela recusa assim que o pedido saiu.
    /// A cópia no pedido é o registro do que foi entregue.
    /// </summary>
    [HttpPost("pedido/atualizar")]
    public async Task<IActionResult> AtualizarNoPedido(
        [FromBody] RequisicaoDeAtualizacaoDeEnderecoDePedido requisicao,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            var endereco = await _enderecos.AtualizarNoPedidoAsync(
                usuarioId.Value,
                requisicao.PedidoId,
                requisicao.Endereco,
                cancellationToken);

            return Ok(endereco);
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

    [HttpPost("excluir")]
    public async Task<IActionResult> Excluir(
        [FromBody] ExclusaoDeEndereco requisicao,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObterId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        try
        {
            await _enderecos.ExcluirAsync(usuarioId.Value, requisicao.EnderecoId, cancellationToken);
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
