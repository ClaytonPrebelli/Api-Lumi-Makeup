using LumiMakeup.Aplicacao.Abstracoes;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/categorias")]
public sealed class CategoriasController : ControllerBase
{
    private readonly IServicoDeCatalogo _servicoDeCatalogo;

    public CategoriasController(IServicoDeCatalogo servicoDeCatalogo)
    {
        _servicoDeCatalogo = servicoDeCatalogo;
    }

    [HttpGet]
    public async Task<IActionResult> ObterTodas(CancellationToken cancellationToken)
    {
        var categorias = await _servicoDeCatalogo.ObterCategoriasAtivasAsync(cancellationToken);
        return Ok(categorias);
    }
}