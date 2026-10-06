using Microsoft.AspNetCore.Mvc;
using Travether.Api.Data;

namespace Travether.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(TravetherDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    public sealed record HealthResponse(string Status, string Database);

    /// <summary>
    /// Liveness for Render. Always 200 while the process is up, so a database blip
    /// doesn't restart the service; the body reports the database separately.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        string database;
        try
        {
            database = await db.Database.CanConnectAsync(ct).ConfigureAwait(false) ? "ok" : "unavailable";
        }
#pragma warning disable CA1031 // Any failure means "unavailable"; the health endpoint itself must not fail.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Database health check failed");
            database = "unavailable";
        }

        return Ok(new HealthResponse("ok", database));
    }
}
