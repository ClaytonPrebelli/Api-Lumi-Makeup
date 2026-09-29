using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/banners")]
public sealed class BannersController : ControllerBase
{
    private readonly ICatalogoService _catalogoService;

    public BannersController(ICatalogoService catalogoService)
    {
        _catalogoService = catalogoService;
    }

    [HttpGet]
    public async Task<IActionResult> ObterAtivos(CancellationToken cancellationToken)
    {
        var banners = await _catalogoService.ObterBannersAtivosAsync(cancellationToken);
        return Ok(banners);
    }
}
