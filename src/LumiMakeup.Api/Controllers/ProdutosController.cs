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

    [HttpGet("{slug}")]
    public async Task<IActionResult> ObterPorSlug(string slug, CancellationToken cancellationToken)
    {
        var produto = await _catalogoService.ObterProdutoPorSlugAsync(slug, cancellationToken);
        return produto is null ? NotFound() : Ok(produto);
    }
}