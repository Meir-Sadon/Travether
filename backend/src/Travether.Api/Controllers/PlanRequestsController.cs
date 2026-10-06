using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Plans;
using Travether.Api.Profiles;

namespace Travether.Api.Controllers;

/// <summary>
/// Joining a plan from outside its card (PLAN.md §4.3, §4.5): alone or with members of your own card.
/// The host and the card's admins approve; approval opens the chat and reveals the meeting point.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class PlanRequestsController(TravetherDbContext db, AccessQueries access, PlanViews views, TimeProvider clock) : ControllerBase
{
    public const int MaxParty = 20;

    private Guid Me => User.RequireUserId();

    [HttpPost("plans/{planId:guid}/requests")]
    public async Task<IActionResult> RequestToJoin(Guid planId, PlanJoinRequestInput input, CancellationToken ct)
    {
        var (planAccess, planCardAccess) = await access.GetPlanAccessAsync(Me, planId, ct).ConfigureAwait(false);
        if (planAccess == PlanAccess.None)
        {
            return ApiError.NotFound();
        }

        var plan = await db.ActivityPlans.AsNoTracking().FirstAsync(p => p.Id == planId, ct).ConfigureAwait(false);
        var party = (input.PartyUserIds ?? []).Where(id => id != Me).Distinct().ToList();
        if (party.Count > MaxParty || (party.Count > 0 && input.SourceCardId is null))
        {
            return ApiError.BadRequest("PartyNotInSourceCard");
        }

        CardAccess? sourceAccess = null;
        var partyInCard = true;
        if (input.SourceCardId is { } sourceId)
        {
            sourceAccess = await access.GetCardAccessAsync(Me, sourceId, ct: ct).ConfigureAwait(false);
            partyInCard = await db.CardMembers.CountAsync(
                m => m.CardId == sourceId && party.Contains(m.UserId) && m.Status == MembershipStatus.Active, ct).ConfigureAwait(false) == party.Count;
        }

        var seatsTaken = await SeatsTakenAsync(planId, ct).ConfigureAwait(false);
        var hasOpen = await db.PlanRequests.AnyAsync(r => r.PlanId == planId && r.RequesterId == Me && r.Status == RequestStatus.Requested, ct).ConfigureAwait(false);
        var denial = AccessRules.CheckPlanJoinRequest(plan, seatsTaken, planAccess, planCardAccess, hasOpen, sourceAccess, partyInCard);
        if (denial != JoinDenial.None)
        {
            return Denied(denial == JoinDenial.NotOpen && plan.Status == PlanStatus.Full ? JoinDenial.Full : denial);
        }

        if (plan.StartsAt <= clock.GetUtcNow())
        {
            return ApiError.Conflict("PlanNotOpen");
        }

        if (seatsTaken + 1 + party.Count > plan.SeatLimit)
        {
            return ApiError.Conflict("NotEnoughSeats");
        }

        if (party.Count > 0 && await db.PlanParticipants.AnyAsync(
            p => p.PlanId == planId && party.Contains(p.UserId) && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false))
        {
            return ApiError.Conflict("PartyAlreadyGoing");
        }

        db.PlanRequests.Add(new PlanRequest
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            RequesterId = Me,
            SourceCardId = input.SourceCardId,
            PartyUserIds = party,
            Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim(),
            Status = RequestStatus.Requested,
            CreatedAt = clock.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("AlreadyRequested");
        }

        return Ok(await views.LoadAsync(planId, Me, null, ct).ConfigureAwait(false));
    }

    /// <summary>Open requests, oldest first, for the host and the card's owner and co-admins.</summary>
    [HttpGet("plans/{planId:guid}/requests")]
    public async Task<IActionResult> OpenRequests(Guid planId, CancellationToken ct)
    {
        if (await RequireManageAsync(planId, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        var rows = await db.PlanRequests.AsNoTracking()
            .Where(r => r.PlanId == planId && r.Status == RequestStatus.Requested && r.Requester.BannedAt == null)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id,
                r.Requester,
                r.PartyUserIds,
                SourceCardName = db.VacationCards.Where(c => c.Id == r.SourceCardId).Select(c => c.Name).FirstOrDefault(),
                r.Message,
                r.Status,
                r.CreatedAt,
            })
            .ToListAsync(ct).ConfigureAwait(false);
        var partyIds = rows.SelectMany(r => r.PartyUserIds).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => partyIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return Ok(rows.Select(r => new PlanRequestDto(
            r.Id,
            PersonDto.From(r.Requester, today),
            r.PartyUserIds.Where(people.ContainsKey).Select(id => PersonDto.From(people[id], today)).ToList(),
            r.SourceCardName,
            r.Message,
            r.Status,
            r.CreatedAt)).ToList());
    }

    /// <summary>
    /// Approves: the requester and their party take seats. A requester or party member who was banned or
    /// became blocked with the host makes the request expire. When the plan fills up, other open requests expire.
    /// </summary>
    [HttpPost("plan-requests/{requestId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid requestId, CancellationToken ct)
    {
        var request = await db.PlanRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, ct).ConfigureAwait(false);
        if (request is null)
        {
            return ApiError.NotFound();
        }

        if (await RequireManageAsync(request.PlanId, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {request.PlanId} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
        var current = await db.PlanRequests.Where(r => r.Id == requestId).Select(r => r.Status).FirstAsync(ct).ConfigureAwait(false);
        if (current != RequestStatus.Requested)
        {
            return ApiError.Conflict("AlreadyDecided");
        }

        if (plan.Status != PlanStatus.Open || plan.StartsAt <= now)
        {
            return ApiError.Conflict("PlanNotOpen");
        }

        var people = request.PartyUserIds.Prepend(request.RequesterId).ToList();
        if (!await AllWelcomeAsync(people, plan.HostId, request, ct).ConfigureAwait(false))
        {
            await SetStatusAsync(requestId, RequestStatus.Expired, now, ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return ApiError.Conflict("RequestExpired");
        }

        var seatsTaken = await SeatsTakenAsync(plan.Id, ct).ConfigureAwait(false);
        var existing = await db.PlanParticipants.Where(p => p.PlanId == plan.Id && people.Contains(p.UserId)).ToListAsync(ct).ConfigureAwait(false);
        var newSeats = people.Count - existing.Count(p => p.Status == MembershipStatus.Active);
        if (seatsTaken + newSeats > plan.SeatLimit)
        {
            return ApiError.Conflict("NotEnoughSeats");
        }

        foreach (var userId in people)
        {
            var row = existing.FirstOrDefault(p => p.UserId == userId);
            if (row is null)
            {
                db.PlanParticipants.Add(new PlanParticipant { PlanId = plan.Id, UserId = userId, SourceCardId = request.SourceCardId, Status = MembershipStatus.Active, JoinedAt = now });
            }
            else if (row.Status != MembershipStatus.Active)
            {
                row.Status = MembershipStatus.Active;
                row.SourceCardId = request.SourceCardId;
                row.JoinedAt = now;
            }
        }

        plan.Status = PlanRules.StatusFor(plan.Status, seatsTaken + newSeats, plan.SeatLimit);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await SetStatusAsync(requestId, RequestStatus.Approved, now, ct).ConfigureAwait(false);
        if (plan.Status == PlanStatus.Full)
        {
            await db.PlanRequests.Where(r => r.PlanId == plan.Id && r.Status == RequestStatus.Requested)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(plan.Id, Me, null, ct).ConfigureAwait(false));
    }

    [HttpPost("plan-requests/{requestId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid requestId, CancellationToken ct)
    {
        var planId = await db.PlanRequests.Where(r => r.Id == requestId).Select(r => (Guid?)r.PlanId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (planId is null)
        {
            return ApiError.NotFound();
        }

        if (await RequireManageAsync(planId.Value, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        return await SetStatusAsync(requestId, RequestStatus.Rejected, clock.GetUtcNow(), ct).ConfigureAwait(false)
            ? Ok(await views.LoadAsync(planId.Value, Me, null, ct).ConfigureAwait(false))
            : ApiError.Conflict("AlreadyDecided");
    }

    /// <summary>The requester takes back an open request.</summary>
    [HttpDelete("plan-requests/{requestId:guid}")]
    public async Task<IActionResult> Withdraw(Guid requestId, CancellationToken ct)
    {
        var changed = await db.PlanRequests
            .Where(r => r.Id == requestId && r.RequesterId == Me && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Withdrawn).SetProperty(r => r.DecidedAt, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
        return changed == 0 ? ApiError.NotFound() : NoContent();
    }

    /// <summary>The host or the card's admins take a participant off the plan. The host stays.</summary>
    [HttpDelete("plans/{planId:guid}/participants/{userId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid planId, Guid userId, CancellationToken ct)
    {
        if (await RequireManageAsync(planId, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {planId} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
        if (userId == plan.HostId)
        {
            return ApiError.BadRequest("HostCannotLeave");
        }

        var changed = await db.PlanParticipants
            .Where(p => p.PlanId == planId && p.UserId == userId && p.Status == MembershipStatus.Active)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, MembershipStatus.Removed), ct).ConfigureAwait(false);
        if (changed == 0)
        {
            return ApiError.NotFound();
        }

        plan.Status = PlanRules.StatusFor(plan.Status, await SeatsTakenAsync(planId, ct).ConfigureAwait(false), plan.SeatLimit);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(planId, Me, null, ct).ConfigureAwait(false));
    }

    private static ObjectResult Denied(JoinDenial denial) => denial switch
    {
        JoinDenial.Blocked => ApiError.NotFound(),
        JoinDenial.AlreadyParticipant => ApiError.Conflict("AlreadyParticipant"),
        JoinDenial.AlreadyRequested => ApiError.Conflict("AlreadyRequested"),
        JoinDenial.OwnCardMembersSelfJoin => ApiError.Conflict("JoinDirectly"),
        JoinDenial.NotOpen => ApiError.Conflict("PlanNotOpen"),
        JoinDenial.Full => ApiError.Conflict("PlanFull"),
        JoinDenial.GroupsOnlyNeedsCard => ApiError.BadRequest("GroupsOnly"),
        JoinDenial.SourceCardNotYours => ApiError.BadRequest("SourceCardNotYours"),
        _ => ApiError.BadRequest("PartyNotInSourceCard"),
    };

    private async Task<IActionResult?> RequireManageAsync(Guid planId, CancellationToken ct)
    {
        var (planAccess, cardAccess) = await access.GetPlanAccessAsync(Me, planId, ct).ConfigureAwait(false);
        if (planAccess == PlanAccess.None)
        {
            return ApiError.NotFound();
        }

        return AccessRules.CanDecidePlanRequests(planAccess, cardAccess) ? null : ApiError.Forbidden();
    }

    private Task<int> SeatsTakenAsync(Guid planId, CancellationToken ct) =>
        db.PlanParticipants.CountAsync(p => p.PlanId == planId && p.Status == MembershipStatus.Active, ct);

    /// <summary>Everyone coming is still an active account, not blocked with the host, and the party is still in the source card.</summary>
    private async Task<bool> AllWelcomeAsync(List<Guid> people, Guid hostId, PlanRequest request, CancellationToken ct)
    {
        foreach (var id in people)
        {
            if (await access.GetActiveUserAsync(id, ct).ConfigureAwait(false) is null
                || await access.IsBlockedEitherWayAsync(id, hostId, ct).ConfigureAwait(false))
            {
                return false;
            }
        }

        if (request.SourceCardId is not { } sourceId || request.PartyUserIds.Count == 0)
        {
            return true;
        }

        var inCard = await db.CardMembers.CountAsync(
            m => m.CardId == sourceId && request.PartyUserIds.Contains(m.UserId) && m.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        return inCard == request.PartyUserIds.Count;
    }

    private async Task<bool> SetStatusAsync(Guid requestId, RequestStatus status, DateTimeOffset now, CancellationToken ct) =>
        await db.PlanRequests
            .Where(r => r.Id == requestId && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, status).SetProperty(r => r.DecidedById, Me).SetProperty(r => r.DecidedAt, now), ct)
            .ConfigureAwait(false) > 0;
}
