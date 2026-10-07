using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Plans;

namespace Travether.Api.Controllers;

/// <summary>
/// Activity Plans (PLAN.md §4.3): created inside a Vacation Card, public to everyone, with the exact
/// meeting point kept for participants. Join requests from outsiders live in step 1.7.
/// </summary>
[ApiController]
[Route("api")]
public sealed class PlansController(TravetherDbContext db, AccessQueries access, PlanViews views, Notifier notifier, NotificationOptions notificationOptions, TimeProvider clock) : ControllerBase
{
    /// <summary>Any member of the card can host. The host may bring card members along as participants.</summary>
    [Authorize]
    [HttpPost("cards/{cardId:guid}/plans")]
    public async Task<IActionResult> Create(Guid cardId, PlanInput input, CancellationToken ct)
    {
        var me = User.RequireUserId();
        var level = await access.GetCardAccessAsync(me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanSeeCardInside(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var card = await db.VacationCards.AsNoTracking().FirstAsync(c => c.Id == cardId, ct).ConfigureAwait(false);
        var others = (input.ParticipantIds ?? []).Where(id => id != me).Distinct().ToList();
        var activeMembers = await db.CardMembers
            .CountAsync(m => m.CardId == cardId && others.Contains(m.UserId) && m.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        // Nobody can be brought into a plan with someone they blocked or who blocked them.
        var blocked = others.Count > 0 && await db.Blocks.AnyAsync(
            b => (b.BlockerId == me && others.Contains(b.BlockedId)) || (b.BlockedId == me && others.Contains(b.BlockerId)), ct).ConfigureAwait(false);
        if (activeMembers != others.Count || blocked)
        {
            return ApiError.BadRequest("NotCardMembers");
        }

        if (!Places.PlaceRules.IsValid(input.Origin.Lat, input.Origin.Lng))
        {
            return ApiError.BadRequest("MeetingPointRequired");
        }

        var zone = PlanRules.TimeZoneAt(input.Origin);
        var startsAt = PlanRules.ToInstant(input.Date, input.Time, zone);
        var now = clock.GetUtcNow();
        if (PlanRules.Validate(input.Title, input.OriginAreaLabel, input.Destination, input.Origin, input.SeatLimit, others.Count + 1, input.Date, startsAt, now, card) is { } error)
        {
            return ApiError.BadRequest(error);
        }

        var plan = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            HostId = me,
            Title = input.Title.Trim(),
            Category = input.Category,
            Origin = PlanRules.ToPoint(input.Origin),
            OriginName = (PlanRules.Clean(input.OriginName) ?? input.OriginAreaLabel).Trim(),
            OriginAreaLabel = input.OriginAreaLabel.Trim(),
            Destination = input.Destination.Trim(),
            DestinationPrecision = input.DestinationPrecision,
            StartsAt = startsAt,
            TimeZoneId = zone,
            Purpose = PlanRules.Clean(input.Purpose),
            SeatLimit = input.SeatLimit,
            Audience = input.Audience,
            Status = PlanRules.StatusFor(PlanStatus.Open, others.Count + 1, input.SeatLimit),
            CreatedAt = now,
            Participants = [.. others.Prepend(me).Select(id => new PlanParticipant { UserId = id, SourceCardId = cardId, Status = MembershipStatus.Active, JoinedAt = now })],
        };
        db.ActivityPlans.Add(plan);
        db.Conversations.Add(new Conversation { Id = Guid.NewGuid(), Type = ConversationType.Plan, RefId = plan.Id, CreatedAt = now });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = plan.Id }, await views.LoadAsync(plan.Id, me, null, ct).ConfigureAwait(false));
    }

    /// <summary>The card's plans, for its members.</summary>
    [Authorize]
    [HttpGet("cards/{cardId:guid}/plans")]
    public async Task<IActionResult> ForCard(Guid cardId, CancellationToken ct)
    {
        var me = User.RequireUserId();
        var level = await access.GetCardAccessAsync(me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanSeeCardInside(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        return Ok(await views.ListForCardAsync(cardId, me, ct).ConfigureAwait(false));
    }

    /// <summary>A plan; public. Pass lat/lng to get a rounded distance from there.</summary>
    [HttpGet("plans/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, double? lat, double? lng, CancellationToken ct)
    {
        var near = lat is { } la && lng is { } ln && Places.PlaceRules.IsValid(la, ln) ? new LatLng(la, ln) : null;
        return await views.LoadAsync(id, User.GetUserId(), near, ct).ConfigureAwait(false) is { } plan ? Ok(plan) : ApiError.NotFound();
    }

    /// <summary>The plan as a calendar event. Same visibility as the plan page: the exact meeting point only for participants.</summary>
    [HttpGet("plans/{id:guid}/calendar.ics")]
    public async Task<IActionResult> Calendar(Guid id, CancellationToken ct)
    {
        if (await views.LoadAsync(id, User.GetUserId(), null, ct).ConfigureAwait(false) is not { } plan)
        {
            return ApiError.NotFound();
        }

        var ics = PlanCalendar.Build(plan, $"{notificationOptions.PublicUrl.TrimEnd('/')}/plans/{plan.Id}", clock.GetUtcNow());
        Response.Headers.CacheControl = "private, no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", $"{PlanCalendar.FileName(plan.Title)}.ics");
    }

    /// <summary>The host, or the card's owner and co-admins, edit the plan.</summary>
    [Authorize]
    [HttpPatch("plans/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, PlanPatch patch, CancellationToken ct)
    {
        var me = User.RequireUserId();
        if (await RequireManageAsync(me, id, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        // Lock the plan row like Join and Approve do, so a seat taken meanwhile is counted against the new limit.
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {id} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
        await db.Entry(plan).Reference(p => p.Card).LoadAsync(ct).ConfigureAwait(false);
        if (plan.Status is PlanStatus.Cancelled or PlanStatus.Done)
        {
            return ApiError.Conflict("PlanClosed");
        }

        var origin = patch.Origin ?? PlanRules.ToLatLng(plan.Origin);
        var zone = patch.Origin is null ? plan.TimeZoneId : PlanRules.TimeZoneAt(origin);
        var (oldDate, oldTime) = PlanRules.ToLocal(plan.StartsAt, plan.TimeZoneId);
        var date = patch.Date ?? oldDate;
        var startsAt = PlanRules.ToInstant(date, patch.Time ?? oldTime, zone);
        var seatLimit = patch.SeatLimit ?? plan.SeatLimit;
        var seatsTaken = await db.PlanParticipants.CountAsync(p => p.PlanId == id && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        var timeChanged = startsAt != plan.StartsAt;
        var error = PlanRules.Validate(
            patch.Title ?? plan.Title, patch.OriginAreaLabel ?? plan.OriginAreaLabel, patch.Destination ?? plan.Destination, origin,
            seatLimit, seatsTaken, date, timeChanged ? startsAt : DateTimeOffset.MaxValue, clock.GetUtcNow(), plan.Card);
        if (!timeChanged && error is "PlanOutsideTrip")
        {
            error = null; // the card's dates moved after the plan was made; leave it be until the time is edited
        }

        if (error is not null)
        {
            return ApiError.BadRequest(error);
        }

        plan.Title = (patch.Title ?? plan.Title).Trim();
        plan.Category = patch.Category ?? plan.Category;
        if (patch.Origin is not null)
        {
            plan.Origin = PlanRules.ToPoint(origin);
        }

        plan.OriginName = (PlanRules.Clean(patch.OriginName) ?? plan.OriginName).Trim();
        plan.OriginAreaLabel = (patch.OriginAreaLabel ?? plan.OriginAreaLabel).Trim();
        plan.Destination = (patch.Destination ?? plan.Destination).Trim();
        plan.DestinationPrecision = patch.DestinationPrecision ?? plan.DestinationPrecision;
        plan.TimeZoneId = zone;
        plan.StartsAt = startsAt;
        if (patch.Purpose is not null)
        {
            plan.Purpose = PlanRules.Clean(patch.Purpose);
        }

        plan.SeatLimit = seatLimit;
        plan.Audience = patch.Audience ?? plan.Audience;
        plan.Status = PlanRules.StatusFor(plan.Status, seatsTaken, seatLimit);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(id, me, null, ct).ConfigureAwait(false));
    }

    /// <summary>Cancels the plan for everyone; open join requests expire.</summary>
    [Authorize]
    [HttpPost("plans/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var me = User.RequireUserId();
        if (await RequireManageAsync(me, id, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var changed = await db.ActivityPlans.Where(p => p.Id == id && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
        if (changed == 0)
        {
            return ApiError.Conflict("PlanClosed");
        }

        await db.PlanRequests.Where(r => r.PlanId == id && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        await notifier.PlanCancelledAsync(id, me, ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(id, me, null, ct).ConfigureAwait(false));
    }

    /// <summary>A member of the plan's card takes a free seat without asking.</summary>
    [Authorize]
    [HttpPost("plans/{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        var me = User.RequireUserId();
        var (planAccess, cardAccess) = await access.GetPlanAccessAsync(me, id, ct).ConfigureAwait(false);
        if (planAccess == PlanAccess.None)
        {
            return ApiError.NotFound();
        }

        if (planAccess >= PlanAccess.Participant)
        {
            return ApiError.Conflict("AlreadyParticipant");
        }

        if (cardAccess < CardAccess.Member)
        {
            return ApiError.Forbidden("NotCardMember");
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Lock the plan row so two people can't take the last seat at once.
        var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {id} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
        var seatsTaken = await db.PlanParticipants.CountAsync(p => p.PlanId == id && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        if (plan.Status != PlanStatus.Open || plan.StartsAt <= now)
        {
            return ApiError.Conflict("PlanNotOpen");
        }

        if (seatsTaken >= plan.SeatLimit)
        {
            return ApiError.Conflict("PlanFull");
        }

        var row = await db.PlanParticipants.FirstOrDefaultAsync(p => p.PlanId == id && p.UserId == me, ct).ConfigureAwait(false);
        if (row is null)
        {
            db.PlanParticipants.Add(new PlanParticipant { PlanId = id, UserId = me, SourceCardId = plan.CardId, Status = MembershipStatus.Active, JoinedAt = now });
        }
        else
        {
            row.Status = MembershipStatus.Active;
            row.SourceCardId = plan.CardId;
            row.JoinedAt = now;
        }

        plan.Status = PlanRules.StatusFor(plan.Status, seatsTaken + 1, plan.SeatLimit);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        await notifier.PlanJoinedAsync(id, me, ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(id, me, null, ct).ConfigureAwait(false));
    }

    /// <summary>A participant gives up their seat. The host cancels the plan instead.</summary>
    [Authorize]
    [HttpPost("plans/{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        var me = User.RequireUserId();
        var (planAccess, _) = await access.GetPlanAccessAsync(me, id, ct).ConfigureAwait(false);
        if (planAccess < PlanAccess.Participant)
        {
            return ApiError.NotFound();
        }

        if (planAccess == PlanAccess.Host)
        {
            return ApiError.Conflict("HostCannotLeave");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {id} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
        await db.PlanParticipants.Where(p => p.PlanId == id && p.UserId == me)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, MembershipStatus.Left), ct).ConfigureAwait(false);
        var seatsTaken = await db.PlanParticipants.CountAsync(p => p.PlanId == id && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        plan.Status = PlanRules.StatusFor(plan.Status, seatsTaken, plan.SeatLimit);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(id, me, null, ct).ConfigureAwait(false));
    }

    private async Task<IActionResult?> RequireManageAsync(Guid me, Guid planId, CancellationToken ct)
    {
        var (planAccess, cardAccess) = await access.GetPlanAccessAsync(me, planId, ct).ConfigureAwait(false);
        if (planAccess == PlanAccess.None)
        {
            return ApiError.NotFound();
        }

        return AccessRules.CanDecidePlanRequests(planAccess, cardAccess) ? null : ApiError.Forbidden();
    }
}
