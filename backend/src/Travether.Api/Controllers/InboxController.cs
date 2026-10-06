using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Controllers;

public enum RequestTarget { Card, Plan }

/// <summary>A join request in the inbox: one waiting for my decision, or one I sent.</summary>
public sealed record InboxRequestDto(
    Guid Id, RequestTarget Target, Guid TargetId, string TargetTitle, PlanCategory? Category,
    PersonDto Person, int PartySize, string? Message, RequestStatus Status, DateTimeOffset CreatedAt);

public sealed record InboxRequestsDto(IReadOnlyList<InboxRequestDto> Incoming, IReadOnlyList<InboxRequestDto> Outgoing);

/// <summary>Inbox (PLAN.md §5 screen 7): join requests waiting for me, and the ones I sent.</summary>
[ApiController]
[Authorize]
[Route("api/inbox")]
public sealed class InboxController(TravetherDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet("requests")]
    public async Task<InboxRequestsDto> Requests(CancellationToken ct)
    {
        var me = User.RequireUserId();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var since = now.AddDays(-30);

        var adminCards = db.CardMembers
            .Where(m => m.UserId == me && m.Status == MembershipStatus.Active && m.Role != CardRole.Member)
            .Select(m => m.CardId);

        var cardIn = await db.CardRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Requested && adminCards.Contains(r.CardId) && r.User.BannedAt == null)
            .Select(r => new { r.Id, r.CardId, r.Card.Name, r.User, r.Message, r.Status, r.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        var planIn = await db.PlanRequests.AsNoTracking()
            .Where(r => r.Status == RequestStatus.Requested && r.Requester.BannedAt == null
                && (r.Plan.HostId == me || adminCards.Contains(r.Plan.CardId)))
            .Select(r => new { r.Id, r.PlanId, r.Plan.Title, r.Plan.Category, r.Requester, Party = r.PartyUserIds.Count, r.Message, r.Status, r.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);

        var meUser = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me, ct).ConfigureAwait(false);
        var cardOut = await db.CardRequests.AsNoTracking()
            .Where(r => r.UserId == me && r.CreatedAt > since)
            .Select(r => new { r.Id, r.CardId, r.Card.Name, r.Message, r.Status, r.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        var planOut = await db.PlanRequests.AsNoTracking()
            .Where(r => r.RequesterId == me && r.CreatedAt > since)
            .Select(r => new { r.Id, r.PlanId, r.Plan.Title, r.Plan.Category, Party = r.PartyUserIds.Count, r.Message, r.Status, r.CreatedAt })
            .ToListAsync(ct).ConfigureAwait(false);

        var incoming = cardIn
            .Select(r => new InboxRequestDto(r.Id, RequestTarget.Card, r.CardId, r.Name, null, PersonDto.From(r.User, today), 1, r.Message, r.Status, r.CreatedAt))
            .Concat(planIn.Select(r => new InboxRequestDto(r.Id, RequestTarget.Plan, r.PlanId, r.Title, r.Category, PersonDto.From(r.Requester, today), r.Party + 1, r.Message, r.Status, r.CreatedAt)))
            .OrderBy(r => r.CreatedAt)
            .ToList();
        var mePerson = PersonDto.From(meUser, today);
        var outgoing = cardOut
            .Select(r => new InboxRequestDto(r.Id, RequestTarget.Card, r.CardId, r.Name, null, mePerson, 1, r.Message, r.Status, r.CreatedAt))
            .Concat(planOut.Select(r => new InboxRequestDto(r.Id, RequestTarget.Plan, r.PlanId, r.Title, r.Category, mePerson, r.Party + 1, r.Message, r.Status, r.CreatedAt)))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
        return new InboxRequestsDto(incoming, outgoing);
    }
}
