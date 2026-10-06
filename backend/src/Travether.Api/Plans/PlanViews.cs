using Microsoft.EntityFrameworkCore;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Plans;

/// <summary>Builds plan DTOs, applying location privacy (PLAN.md §4.3) through AccessQueries.</summary>
public sealed class PlanViews(TravetherDbContext db, AccessQueries access, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>The plan as the viewer may see it, or null when they may not see it at all.</summary>
    public async Task<PlanDto?> LoadAsync(Guid planId, Guid? viewerId, LatLng? near, CancellationToken ct)
    {
        var (planAccess, cardAccess) = await access.GetPlanAccessAsync(viewerId, planId, ct).ConfigureAwait(false);
        if (planAccess == PlanAccess.None)
        {
            return null;
        }

        var location = (await access.GetPlanLocationAsync(viewerId, planId, near is null ? null : PlanRules.ToPoint(near), ct).ConfigureAwait(false))!;
        var row = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => new
            {
                Plan = p,
                p.Host,
                CardName = p.Card.Name,
                Participants = p.Participants
                    .Where(x => x.Status == MembershipStatus.Active)
                    .OrderBy(x => x.UserId != p.HostId)
                    .ThenBy(x => x.JoinedAt)
                    .Select(x => x.User)
                    .ToList(),
            })
            .FirstAsync(ct).ConfigureAwait(false);

        var plan = row.Plan;
        var today = Today;
        var seatsTaken = row.Participants.Count;
        var (localDate, localTime) = PlanRules.ToLocal(plan.StartsAt, plan.TimeZoneId);
        var isInsider = planAccess >= PlanAccess.Participant || cardAccess >= CardAccess.Member;
        return new PlanDto(
            plan.Id,
            plan.CardId,
            cardAccess >= CardAccess.Preview ? row.CardName : null,
            plan.Title,
            plan.Category,
            plan.StartsAt,
            plan.TimeZoneId,
            localDate,
            localTime,
            location.AreaLabel,
            location.Distance is { } d ? new DistanceDto(d.Km, d.UnderOneKm) : null,
            location.ExactOrigin is { } o ? new MeetingPointDto(plan.OriginName, o.Y, o.X) : null,
            location.Destination,
            plan.DestinationPrecision,
            plan.Purpose,
            plan.SeatLimit,
            seatsTaken,
            plan.Audience,
            plan.Status,
            JsonNamingPolicyName(planAccess),
            AccessRules.CanDecidePlanRequests(planAccess, cardAccess),
            CanSelfJoin(plan, planAccess, cardAccess, seatsTaken, clock.GetUtcNow()),
            PersonDto.From(row.Host, today),
            isInsider ? row.Participants.Select(u => PersonDto.From(u, today)).ToList() : null);
    }

    /// <summary>A card's plans for its members: upcoming first, then past; cancelled plans left out.</summary>
    public async Task<IReadOnlyList<PlanSummaryDto>> ListForCardAsync(Guid cardId, Guid viewerId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = Today;
        var rows = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.CardId == cardId && p.Status != PlanStatus.Cancelled)
            .Select(p => new
            {
                Plan = p,
                p.Host,
                SeatsTaken = p.Participants.Count(x => x.Status == MembershipStatus.Active),
                Joined = p.Participants.Any(x => x.UserId == viewerId && x.Status == MembershipStatus.Active),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .OrderBy(r => r.Plan.StartsAt < now)
            .ThenBy(r => r.Plan.StartsAt < now ? -r.Plan.StartsAt.UtcTicks : r.Plan.StartsAt.UtcTicks)
            .Select(r => Summary(r.Plan, r.Host, r.SeatsTaken, r.Joined, today))
            .ToList();
    }

    public static PlanSummaryDto Summary(ActivityPlan p, User host, int seatsTaken, bool joined, DateOnly today)
    {
        var (date, time) = PlanRules.ToLocal(p.StartsAt, p.TimeZoneId);
        return new PlanSummaryDto(p.Id, p.Title, p.Category, p.StartsAt, p.TimeZoneId, date, time, p.OriginAreaLabel,
            p.SeatLimit, seatsTaken, p.Audience, p.Status, PersonDto.From(host, today), joined);
    }

    /// <summary>Members of the plan's own card join without asking while seats are free (PLAN.md §4.3).</summary>
    public static bool CanSelfJoin(ActivityPlan plan, PlanAccess planAccess, CardAccess cardAccess, int seatsTaken, DateTimeOffset now) =>
        cardAccess >= CardAccess.Member && planAccess < PlanAccess.Participant
        && plan.Status == PlanStatus.Open && seatsTaken < plan.SeatLimit && plan.StartsAt > now;

    private static string JsonNamingPolicyName(PlanAccess a) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(a.ToString());
}
