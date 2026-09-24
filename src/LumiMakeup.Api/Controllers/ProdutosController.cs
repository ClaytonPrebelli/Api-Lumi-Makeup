using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/produtos")]
public sealed class ProdutosController : ControllerBase
{
    private readonly IServicoDeCatalogo _servicoDeCatalogo;

    public ProdutosController(IServicoDeCatalogo servicoDeCatalogo)
    {
        _servicoDeCatalogo = servicoDeCatalogo;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodos(CancellationToken cancellationToken)
    {
        var produtos = await _servicoDeCatalogo.ObterProdutosAtivosAsync(cancellationToken);
        return Ok(produtos);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> ObterPorSlug(string slug, CancellationToken cancellationToken)
    {
        var produto = await _servicoDeCatalogo.ObterProdutoPorSlugAsync(slug, cancellationToken);
        return produto is null ? NotFound() : Ok(produto);
    }
}