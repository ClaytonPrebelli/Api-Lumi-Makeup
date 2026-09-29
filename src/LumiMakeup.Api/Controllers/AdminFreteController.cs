using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

/*
 * VERBOS: mesma regra de AdminProdutosController. Salvar e simular sao POST,
 * porque o servidor de producao nao encaminha PUT.
 */
[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/frete")]
public sealed class AdminFreteController : ControllerBase
{
    private readonly IGestaoDeFreteService _frete;

    public AdminFreteController(IGestaoDeFreteService frete)
    {
        _frete = frete;
    }

    /// <summary>
    /// A configuração atual, ou uma vazia. Vazio é o estado inicial de uma loja
    /// que ainda não vendeu pelo site, e a tela mostra o formulário para isso.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        var configuracao = await _frete.ObterAsync(cancellationToken);
        return Ok(configuracao);
    }

    [HttpPost]
    public async Task<IActionResult> Salvar(
        [FromBody] RequisicaoDeConfiguracaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var configuracao = await _frete.SalvarAsync(requisicao, cancellationToken);
            return Ok(configuracao);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Simula o frete de um CEP, sem criar pedido.</summary>
    [HttpPost("simular")]
    public async Task<IActionResult> Simular(
        [FromBody] RequisicaoDeSimulacaoDeFrete requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var calculo = await _frete.SimularAsync(requisicao.Cep, cancellationToken);
            return Ok(calculo);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
