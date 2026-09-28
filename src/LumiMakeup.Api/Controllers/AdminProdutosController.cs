using LumiMakeup.Application.Abstractions;
using LumiMakeup.Application.DTOs;
using LumiMakeup.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Authorize(Policy = "SomenteAdministrador")]
[Route("api/admin/produtos")]
public sealed class AdminProdutosController : ControllerBase
{
    private readonly IGestaoDeProdutosService _gestaoDeProdutos;
    private readonly IMelhoradorDeTextoService _melhoradorDeTexto;
    private readonly ArmazenamentoDeImagensOptions _opcoes;

    public AdminProdutosController(
        IGestaoDeProdutosService gestaoDeProdutos,
        IMelhoradorDeTextoService melhoradorDeTexto,
        IOptions<ArmazenamentoDeImagensOptions> opcoes)
    {
        _gestaoDeProdutos = gestaoDeProdutos;
        _melhoradorDeTexto = melhoradorDeTexto;
        _opcoes = opcoes.Value;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
    {
        var produtos = await _gestaoDeProdutos.ObterTodosAsync(cancellationToken);
        return Ok(produtos);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> ObterPorId(long id, CancellationToken cancellationToken)
    {
        var produto = await _gestaoDeProdutos.ObterPorIdAsync(id, cancellationToken);
        return produto is null ? NotFound() : Ok(produto);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        [FromBody] RequisicaoDeProduto requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var produto = await _gestaoDeProdutos.CriarAsync(requisicao, cancellationToken);
            return CreatedAtAction(nameof(ObterPorId), new { id = produto.Id }, produto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Atualizar(
        long id,
        [FromBody] RequisicaoDeProduto requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var produto = await _gestaoDeProdutos.AtualizarAsync(id, requisicao, cancellationToken);
            return Ok(produto);
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
            await _gestaoDeProdutos.ExcluirAsync(id, cancellationToken);
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

    [HttpPost("texto/melhorar")]
    public async Task<IActionResult> MelhorarTexto(
        [FromBody] RequisicaoDeMelhoriaDeTexto requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _melhoradorDeTexto.MelhorarAsync(
                requisicao.Nome ?? string.Empty,
                requisicao.Descricao,
                cancellationToken);

            return Ok(new RespostaDeMelhoriaDeTextoDto(resultado.DescricaoMelhorada, resultado.ModeloUsado));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/imagens")]
    [RequestSizeLimit(6_291_456)]
    public async Task<IActionResult> AdicionarImagem(long id, IFormFile arquivo, CancellationToken cancellationToken)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { message = "Selecione um arquivo de imagem." });
        }

        if (arquivo.Length > _opcoes.TamanhoMaximoEmBytes)
        {
            return BadRequest(new
            {
                message = $"A imagem excede o limite de {_opcoes.TamanhoMaximoEmBytes / (1024 * 1024)} MB."
            });
        }

        try
        {
            await using var conteudo = arquivo.OpenReadStream();
            var imagem = await _gestaoDeProdutos.AdicionarImagemAsync(id, conteudo, arquivo.FileName, cancellationToken);
            return CreatedAtAction(nameof(ObterPorId), new { id }, imagem);
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

    [HttpDelete("{id:long}/imagens/{imagemId:long}")]
    public async Task<IActionResult> ExcluirImagem(
        long id,
        long imagemId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _gestaoDeProdutos.ExcluirImagemAsync(id, imagemId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Antes so o KeyNotFoundException era tratado, e qualquer outra falha
            // virava 500 sem mensagem. Apagar uma imagem nao pode falhar por causa
            // do arquivo: e a referencia no banco que precisa sair.
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}/imagens/ordem")]
    public async Task<IActionResult> ReordenarImagens(
        long id,
        [FromBody] RequisicaoDeOrdenacaoDeImagens requisicao,
        CancellationToken cancellationToken)
    {
        try
        {
            var imagens = await _gestaoDeProdutos.ReordenarImagensAsync(id, requisicao.Ordem, cancellationToken);
            return Ok(imagens);
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
