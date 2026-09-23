using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public ProductsController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var products = await _catalogService.GetActiveProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var product = await _catalogService.GetProductBySlugAsync(slug, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }
}