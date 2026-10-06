using Microsoft.AspNetCore.Mvc;

namespace Travether.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    public sealed record HealthResponse(string Status);

    [HttpGet]
    public ActionResult<HealthResponse> Get() => Ok(new HealthResponse("ok"));
}
