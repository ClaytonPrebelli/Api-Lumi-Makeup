using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/saude")]
public sealed class SaudeController : ControllerBase
{
    [HttpGet]
    public IActionResult Obter() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}