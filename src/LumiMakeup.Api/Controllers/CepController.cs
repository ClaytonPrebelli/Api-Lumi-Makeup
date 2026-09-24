using LumiMakeup.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/cep")]
public sealed class CepController : ControllerBase
{
    private readonly IViaCepService _viaCepService;

    public CepController(IViaCepService viaCepService)
    {
        _viaCepService = viaCepService;
    }

    [HttpGet("{cep}")]
    public async Task<IActionResult> Consultar(string cep, CancellationToken cancellationToken)
    {
        var resultado = await _viaCepService.ConsultarAsync(cep, cancellationToken);
        if (resultado is null)
        {
            return NotFound();
        }

        return Ok(resultado);
    }
}