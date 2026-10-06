using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Places;
using Travether.Api.Plans;

namespace Travether.Api.Controllers;

public enum DiscoverSort { Distance, Date }

/// <summary>A plan found by Discover, with its rounded public distance from the searcher's origin.</summary>
public sealed record DiscoverPlanDto(PlanSummaryDto Plan, DistanceDto Distance);

/// <summary>
/// Discover (PLAN.md §4.4): open plans with free seats near an origin, on dates within the searcher's trip.
/// Distances use the grid-snapped public point, like the plan page.
/// </summary>
[ApiController]
[Route("api/discover")]
public sealed class DiscoverController(TravetherDbContext db, AccessQueries access, TimeProvider clock, IConfiguration config) : ControllerBase
{
    public const int MaxRadiusKm = 100;
    public const int MaxResults = 100;
    private const int MaxDays = 366;

    [HttpGet]
    public async Task<IActionResult> Search(
        double lat, double lng, DateOnly from, DateOnly to,
        int? radiusKm, PlanCategory? category, int? maxSeats, DiscoverSort sort = DiscoverSort.Distance,
        CancellationToken ct = default)
    {
        if (!PlaceRules.IsValid(lat, lng))
        {
            return ApiError.BadRequest("InvalidLocation");
        }

        if (to < from || to.DayNumber - from.DayNumber > MaxDays)
        {
            return ApiError.BadRequest("DatesOutOfOrder");
        }

        var radius = Math.Clamp(radiusKm ?? config.GetValue("Discover:DefaultRadiusKm", 30), 1, MaxRadiusKm);
        var origin = PlanRules.ToPoint(new LatLng(lat, lng));
        var now = clock.GetUtcNow();

        // Plans store instants; compare local dates after a generous UTC window (zones span UTC−12…+14).
        var windowStart = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-14);
        var windowEnd = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(12);

        var plans = db.ActivityPlans.AsNoTracking()
            .Where(p => p.Status == PlanStatus.Open && p.StartsAt > now && p.StartsAt >= windowStart && p.StartsAt < windowEnd)
            .Where(p => p.Host.BannedAt == null && p.Host.DeletedAt == null)
            .Where(p => p.OriginPublic.IsWithinDistance(origin, radius * 1000.0));

        if (await access.GetActiveUserAsync(User.GetUserId(), ct).ConfigureAwait(false) is { } viewer)
        {
            var me = viewer.Id;

            // Your own trips' plans are on the trip page; people you blocked, or who blocked you, stay out of sight.
            plans = plans
                .Where(p => !db.CardMembers.Any(m => m.CardId == p.CardId && m.UserId == me && m.Status == MembershipStatus.Active))
                .Where(p => !p.Participants.Any(x => x.UserId == me && x.Status == MembershipStatus.Active))
                .Where(p => !db.Blocks.Any(b => (b.BlockerId == me && b.BlockedId == p.HostId) || (b.BlockerId == p.HostId && b.BlockedId == me)));
        }

        if (category is { } c)
        {
            plans = plans.Where(p => p.Category == c);
        }

        if (maxSeats is { } max)
        {
            plans = plans.Where(p => p.SeatLimit <= max);
        }

        var rows = await plans
            .Select(p => new
            {
                Plan = p,
                p.Host,
                SeatsTaken = p.Participants.Count(x => x.Status == MembershipStatus.Active),
                Meters = p.OriginPublic.Distance(origin),
            })
            .OrderBy(r => r.Meters)
            .Take(MaxResults * 2)
            .ToListAsync(ct).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var found = rows
            .Where(r => r.SeatsTaken < r.Plan.SeatLimit)
            .Select(r => (Row: r, Summary: PlanViews.Summary(r.Plan, r.Host, r.SeatsTaken, joined: false, today), Distance: AccessRules.RoundDistance(r.Meters)))
            .Where(x => x.Summary.LocalDate >= from && x.Summary.LocalDate <= to);

        found = sort == DiscoverSort.Date
            ? found.OrderBy(x => x.Row.Plan.StartsAt).ThenBy(x => x.Row.Meters)
            : found.OrderBy(x => x.Distance.UnderOneKm ? 0 : x.Distance.Km).ThenBy(x => x.Row.Plan.StartsAt);

        return Ok(found
            .Take(MaxResults)
            .Select(x => new DiscoverPlanDto(x.Summary, new DistanceDto(x.Distance.Km, x.Distance.UnderOneKm)))
            .ToList());
    }
}
