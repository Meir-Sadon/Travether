using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Travether.Api.Api;
using Travether.Api.Places;

namespace Travether.Api.Controllers;

/// <summary>Meeting-point search for plan forms. Proxied so the provider and its key stay on the server.</summary>
[ApiController]
[Authorize]
[Route("api/places")]
[EnableRateLimiting(PlaceSetup.RateLimit)]
public sealed class PlacesController(IPlaceSearch places, ILogger<PlacesController> log) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, double? lat, double? lng, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return Ok(Array.Empty<PlaceDto>());
        }

        try
        {
            return Ok(await places.SearchAsync(q.Trim()[..Math.Min(q.Trim().Length, 120)], lat, lng, ct).ConfigureAwait(false));
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Place search failed");
            return ApiError.Create(StatusCodes.Status503ServiceUnavailable, "PlacesUnavailable");
        }
    }

    [HttpGet("reverse")]
    public async Task<IActionResult> Reverse(double lat, double lng, CancellationToken ct)
    {
        if (!PlaceRules.IsValid(lat, lng))
        {
            return ApiError.BadRequest("InvalidLocation");
        }

        try
        {
            return await places.ReverseAsync(lat, lng, ct).ConfigureAwait(false) is { } place ? Ok(place) : ApiError.NotFound();
        }
        catch (HttpRequestException ex)
        {
            log.LogWarning(ex, "Reverse geocoding failed");
            return ApiError.Create(StatusCodes.Status503ServiceUnavailable, "PlacesUnavailable");
        }
    }
}
