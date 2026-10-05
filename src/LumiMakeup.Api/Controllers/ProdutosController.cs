using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/produtos")]
public sealed class ProdutosController : ControllerBase
{
    private readonly ICatalogoService _catalogoService;

    public ProdutosController(ICatalogoService catalogoService)
    {
        _catalogoService = catalogoService;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
    {
        var produtos = await _catalogoService.ObterProdutosAtivosAsync(cancellationToken);
        return Ok(produtos);
    }

    [HttpGet("paginados")]
    public async Task<IActionResult> ObterPaginados(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 12,
        [FromQuery] string? categoriaSlug = null,
        CancellationToken cancellationToken = default)
    {
        if (pagina < 1 || tamanhoPagina is < 1 or > 100 ||
            pagina > int.MaxValue / tamanhoPagina)
        {
            return BadRequest(new { message = "Informe uma página positiva e um tamanho de página entre 1 e 100." });
        }

        var produtos = await _catalogoService.ObterProdutosPaginadosAsync(
            pagina,
            tamanhoPagina,
            categoriaSlug,
            cancellationToken);

        return Ok(produtos);
    }

    [HttpGet("destaques")]
    public async Task<IActionResult> ObterDestaques(CancellationToken cancellationToken)
    {
        var produtos = await _catalogoService.ObterProdutosDestaqueAsync(cancellationToken);
        return Ok(produtos);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> ObterPorSlug(string slug, CancellationToken cancellationToken)
    {
        var produto = await _catalogoService.ObterProdutoPorSlugAsync(slug, cancellationToken);
        return produto is null ? NotFound() : Ok(produto);
    }
}