using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Profiles;

namespace Travether.Api.Controllers;

/// <summary>Other travelers' profiles. Field visibility comes from AccessQueries.GetProfileAccessAsync.</summary>
[ApiController]
[Route("api/users")]
public sealed class UsersController(TravetherDbContext db, AccessQueries access, RatingQueries ratings, TimeProvider clock) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var level = await access.GetProfileAccessAsync(User.GetUserId(), id, ct).ConfigureAwait(false);
        if (level == ProfileAccess.None)
        {
            return ApiError.NotFound(); // blocked, banned and deleted look the same as missing
        }

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == id, ct).ConfigureAwait(false);
        var rating = await ratings.GetAsync(id, ct).ConfigureAwait(false);
        return Ok(PublicProfileDto.From(user, level, rating, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)));
    }
}
