using Microsoft.AspNetCore.Mvc;
using Travether.Api.Telemetry;

namespace Travether.Api.Controllers;

[ApiController]
[Route("api/client-config")]
public sealed class ClientConfigController(TelemetryOptions telemetry) : ControllerBase
{
    /// <summary>Public keys the browser app starts with. Analytics still waits for the user's consent.</summary>
    [HttpGet]
    public ClientConfigDto Get() => new(
        string.IsNullOrWhiteSpace(telemetry.PostHogKey) ? null : telemetry.PostHogKey,
        telemetry.PostHogHost,
        string.IsNullOrWhiteSpace(telemetry.SentryDsn) ? null : telemetry.SentryDsn,
        telemetry.Environment);
}
