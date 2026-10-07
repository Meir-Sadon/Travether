using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Cards;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Profiles;
using Travether.Api.Safety;

namespace Travether.Api.Controllers;

/// <summary>
/// Joining and leaving Vacation Cards (PLAN.md §4.2, §4.5): anyone with preview access may request,
/// the owner and co-admins decide, the owner appoints co-admins and removes members, members leave.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class CardMembershipController(TravetherDbContext db, AccessQueries access, CardViews views, Notifier notifier, TimeProvider clock) : ControllerBase
{
    private Guid Me => User.RequireUserId();

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>Asks to join. Invite-only cards need the share slug the requester opened.</summary>
    [HttpPost("cards/{cardId:guid}/requests")]
    [EnableRateLimiting(SafetySetup.JoinRequestsRateLimit)]
    public async Task<IActionResult> RequestToJoin(Guid cardId, CardJoinRequestInput input, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(Me, cardId, input.ShareSlug, ct).ConfigureAwait(false);
        if (level == CardAccess.None)
        {
            return ApiError.NotFound();
        }

        if (!AccessRules.CanRequestToJoinCard(level))
        {
            return ApiError.Conflict("AlreadyMember");
        }

        var endsOn = await db.VacationCards.Where(c => c.Id == cardId).Select(c => c.EndsOn).FirstAsync(ct).ConfigureAwait(false);
        if (endsOn < Today)
        {
            return ApiError.Conflict("TripEnded");
        }

        var request = new CardRequest
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = Me,
            Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim(),
            Status = RequestStatus.Requested,
            CreatedAt = clock.GetUtcNow(),
        };
        db.CardRequests.Add(request);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("AlreadyRequested");
        }

        await notifier.CardRequestedAsync(cardId, Me, ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(cardId, level, Me, ct).ConfigureAwait(false));
    }

    /// <summary>Open requests, oldest first, for the owner and co-admins.</summary>
    [HttpGet("cards/{cardId:guid}/requests")]
    public async Task<IActionResult> OpenRequests(Guid cardId, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(Me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanDecideCardRequests(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var today = Today;
        var rows = await db.CardRequests.AsNoTracking()
            .Where(r => r.CardId == cardId && r.Status == RequestStatus.Requested && r.User.BannedAt == null)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Id, r.User, r.Message, r.Status, r.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        return Ok(rows.Select(r => new CardRequestDto(r.Id, PersonDto.From(r.User, today), r.Message, r.Status, r.CreatedAt)).ToList());
    }

    [HttpPost("card-requests/{requestId:guid}/approve")]
    public Task<IActionResult> Approve(Guid requestId, CancellationToken ct) => DecideAsync(requestId, approve: true, ct);

    [HttpPost("card-requests/{requestId:guid}/reject")]
    public Task<IActionResult> Reject(Guid requestId, CancellationToken ct) => DecideAsync(requestId, approve: false, ct);

    /// <summary>The requester takes back an open request.</summary>
    [HttpDelete("card-requests/{requestId:guid}")]
    public async Task<IActionResult> Withdraw(Guid requestId, CancellationToken ct)
    {
        var changed = await db.CardRequests
            .Where(r => r.Id == requestId && r.UserId == Me && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Withdrawn).SetProperty(r => r.DecidedAt, clock.GetUtcNow()), ct)
            .ConfigureAwait(false);
        return changed == 0 ? ApiError.NotFound() : NoContent();
    }

    /// <summary>Owner only: make a member co-admin or member, or hand over ownership (the old owner becomes co-admin).</summary>
    [HttpPut("cards/{cardId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> SetRole(Guid cardId, Guid userId, MemberRoleInput input, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(Me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        if (userId == Me)
        {
            return ApiError.BadRequest("CannotChangeOwnRole");
        }

        var target = await db.CardMembers.FirstOrDefaultAsync(m => m.CardId == cardId && m.UserId == userId && m.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        if (target is null)
        {
            return ApiError.NotFound();
        }

        if (input.Role == CardRole.Owner)
        {
            // One owner per card (unique index): demote first, then promote, in one transaction.
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await db.CardMembers.Where(m => m.CardId == cardId && m.UserId == Me)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, CardRole.CoAdmin), ct).ConfigureAwait(false);
            await db.CardMembers.Where(m => m.CardId == cardId && m.UserId == userId)
                .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, CardRole.Owner), ct).ConfigureAwait(false);
            await db.VacationCards.Where(c => c.Id == cardId)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.OwnerId, userId), ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return Ok(await views.LoadAsync(cardId, CardAccess.CoAdmin, Me, ct).ConfigureAwait(false));
        }

        target.Role = input.Role;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(await views.LoadAsync(cardId, level, Me, ct).ConfigureAwait(false));
    }

    /// <summary>Owner only: removes a member. They lose the card, its chat and the card's plans at once.</summary>
    [HttpDelete("cards/{cardId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> Remove(Guid cardId, Guid userId, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(Me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        if (userId == Me)
        {
            return ApiError.BadRequest("OwnerCannotLeave");
        }

        return await EndMembershipAsync(cardId, userId, MembershipStatus.Removed, ct).ConfigureAwait(false)
            ? Ok(await views.LoadAsync(cardId, level, Me, ct).ConfigureAwait(false))
            : ApiError.NotFound();
    }

    /// <summary>A member or co-admin leaves. The owner hands over ownership or deletes the card instead.</summary>
    [HttpPost("cards/{cardId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid cardId, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(Me, cardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanSeeCardInside(level))
        {
            return ApiError.NotFound();
        }

        if (level == CardAccess.Owner)
        {
            return ApiError.Conflict("OwnerCannotLeave");
        }

        await EndMembershipAsync(cardId, Me, MembershipStatus.Left, ct).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<IActionResult> DecideAsync(Guid requestId, bool approve, CancellationToken ct)
    {
        var request = await db.CardRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, ct).ConfigureAwait(false);
        if (request is null)
        {
            return ApiError.NotFound();
        }

        var level = await access.GetCardAccessAsync(Me, request.CardId, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanDecideCardRequests(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Conditional update: two co-admins deciding at once can't both win.
        var status = approve ? RequestStatus.Approved : RequestStatus.Rejected;
        var ownerId = await db.VacationCards.Where(c => c.Id == request.CardId).Select(c => c.OwnerId).FirstAsync(ct).ConfigureAwait(false);
        if (approve && (await access.GetActiveUserAsync(request.UserId, ct).ConfigureAwait(false) is null
            || await access.IsBlockedEitherWayAsync(request.UserId, ownerId, ct).ConfigureAwait(false)))
        {
            // The requester was banned, deleted or a block appeared since: the request lapses.
            status = RequestStatus.Expired;
        }

        var changed = await db.CardRequests
            .Where(r => r.Id == requestId && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, status).SetProperty(r => r.DecidedById, Me).SetProperty(r => r.DecidedAt, now), ct)
            .ConfigureAwait(false);
        if (changed == 0)
        {
            return ApiError.Conflict("AlreadyDecided");
        }

        if (status == RequestStatus.Approved)
        {
            var member = await db.CardMembers.IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.CardId == request.CardId && m.UserId == request.UserId, ct).ConfigureAwait(false);
            if (member is null)
            {
                db.CardMembers.Add(new CardMember { CardId = request.CardId, UserId = request.UserId, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = now });
            }
            else
            {
                // Coming back after leaving or being removed: a fresh membership.
                member.Status = MembershipStatus.Active;
                member.Role = CardRole.Member;
                member.JoinedAt = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        if (status != RequestStatus.Expired)
        {
            await notifier.CardRequestDecidedAsync(request.CardId, request.UserId, status == RequestStatus.Approved, ct).ConfigureAwait(false);
        }

        return status == RequestStatus.Expired
            ? ApiError.Conflict("RequestExpired")
            : Ok(await views.LoadAsync(request.CardId, level, Me, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Ends a membership and the participations it brought: plans of this card joined as its member.
    /// Plans they joined from their own card through a request stay theirs. Upcoming plans they host
    /// in this card are cancelled, so a removed member keeps no host powers over the group.
    /// </summary>
    private async Task<bool> EndMembershipAsync(Guid cardId, Guid userId, MembershipStatus status, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        List<Guid> cancelled = [];
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var changed = await db.CardMembers
            .Where(m => m.CardId == cardId && m.UserId == userId && m.Status == MembershipStatus.Active && m.Role != CardRole.Owner)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, status), ct).ConfigureAwait(false);
        if (changed > 0)
        {
            await db.PlanParticipants
                .Where(p => p.UserId == userId && p.SourceCardId == cardId && p.Plan.CardId == cardId && p.Status == MembershipStatus.Active)
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, status), ct).ConfigureAwait(false);

            cancelled = await db.ActivityPlans
                .Where(p => p.CardId == cardId && p.HostId == userId && p.StartsAt > now && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
                .Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false);
            await db.ActivityPlans.Where(p => cancelled.Contains(p.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
            await db.PlanRequests.Where(r => cancelled.Contains(r.PlanId) && r.Status == RequestStatus.Requested)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        }

        await tx.CommitAsync(ct).ConfigureAwait(false);
        foreach (var planId in cancelled)
        {
            await notifier.PlanCancelledAsync(planId, userId, ct).ConfigureAwait(false);
        }

        return changed > 0;
    }
}
