using Microsoft.AspNetCore.Mvc;

namespace LumiMakeup.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}