using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CategoriesController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var categories = await _catalogService.GetActiveCategoriesAsync(cancellationToken);
        return Ok(categories);
    }
}