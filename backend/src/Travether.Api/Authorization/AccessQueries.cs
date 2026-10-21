using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Authorization;

/// <summary>What a viewer may know about a plan's location.</summary>
/// <param name="AreaLabel">Always present.</param>
/// <param name="Distance">Rounded distance from the viewer's origin to the public (grid-snapped) point, when an origin was given.</param>
/// <param name="ExactOrigin">Only for participants and the host.</param>
/// <param name="Destination">Exact destinations only for participants; regional ones for everyone.</param>
public sealed record PlanLocationView(string AreaLabel, ApproxDistance? Distance, Point? ExactOrigin, string? Destination);

/// <summary>
/// Loads the facts <see cref="AccessRules"/> decides on. A null viewer is a signed-out visitor;
/// a banned or deleted viewer is treated as a visitor too.
/// </summary>
public sealed class AccessQueries(TravetherDbContext db)
{
    public async Task<User?> GetActiveUserAsync(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null)
        {
            return null;
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        return user is not null && AccessRules.IsActive(user) ? user : null;
    }

    public Task<bool> IsBlockedEitherWayAsync(Guid a, Guid b, CancellationToken ct = default) =>
        db.Blocks.AnyAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a), ct);

    /// <summary>True when both are active members of the same card or active participants of the same plan.</summary>
    public async Task<bool> AreCoParticipantsAsync(Guid a, Guid b, CancellationToken ct = default)
    {
        var sharedCard = await db.CardMembers
            .Where(m => m.UserId == a && m.Status == MembershipStatus.Active)
            .AnyAsync(m => db.CardMembers.Any(o => o.CardId == m.CardId && o.UserId == b && o.Status == MembershipStatus.Active), ct)
            .ConfigureAwait(false);
        if (sharedCard)
        {
            return true;
        }

        return await db.PlanParticipants
            .Where(p => p.UserId == a && p.Status == MembershipStatus.Active)
            .AnyAsync(p => db.PlanParticipants.Any(o => o.PlanId == p.PlanId && o.UserId == b && o.Status == MembershipStatus.Active), ct)
            .ConfigureAwait(false);
    }

    public async Task<ProfileAccess> GetProfileAccessAsync(Guid? viewerId, Guid targetId, CancellationToken ct = default)
    {
        var target = await GetActiveUserAsync(targetId, ct).ConfigureAwait(false);
        if (target is null)
        {
            return ProfileAccess.None;
        }

        var viewer = await GetActiveUserAsync(viewerId, ct).ConfigureAwait(false);
        if (viewer is null)
        {
            return ProfileAccess.Public;
        }

        if (viewer.Id == targetId)
        {
            return ProfileAccess.Self;
        }

        var blocked = await IsBlockedEitherWayAsync(viewer.Id, targetId, ct).ConfigureAwait(false);
        var coParticipants = !blocked && await AreCoParticipantsAsync(viewer.Id, targetId, ct).ConfigureAwait(false);
        return AccessRules.ProfileAccessFor(viewer.Id, viewer.Role, targetId, blocked, coParticipants);
    }

    /// <param name="shareSlug">The slug from the share link the viewer opened, if any.</param>
    public async Task<CardAccess> GetCardAccessAsync(Guid? viewerId, Guid cardId, string? shareSlug = null, CancellationToken ct = default)
    {
        var card = await db.VacationCards.AsNoTracking()
            .Where(c => c.Id == cardId)
            .Select(c => new { c.Visibility, c.ShareSlug, c.OwnerId, OwnerActive = c.Owner.BannedAt == null })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (card is null || !card.OwnerActive)
        {
            return CardAccess.None;
        }

        var viaShareLink = shareSlug is not null && string.Equals(shareSlug, card.ShareSlug, StringComparison.Ordinal);
        var viewer = await GetActiveUserAsync(viewerId, ct).ConfigureAwait(false);
        if (viewer is null)
        {
            return AccessRules.CardAccessFor(card.Visibility, activeRole: null, viaShareLink, blockedByOwner: false);
        }

        var role = await db.CardMembers.AsNoTracking()
            .Where(m => m.CardId == cardId && m.UserId == viewer.Id && m.Status == MembershipStatus.Active)
            .Select(m => (CardRole?)m.Role)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var blocked = role is null && await IsBlockedEitherWayAsync(viewer.Id, card.OwnerId, ct).ConfigureAwait(false);
        return AccessRules.CardAccessFor(card.Visibility, role, viaShareLink, blocked);
    }

    /// <summary>The viewer's access to a plan, and to the card the plan belongs to.</summary>
    public async Task<(PlanAccess Plan, CardAccess Card)> GetPlanAccessAsync(Guid? viewerId, Guid planId, CancellationToken ct = default)
    {
        var plan = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => new { p.HostId, p.CardId, HostActive = p.Host.BannedAt == null })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (plan is null || !plan.HostActive)
        {
            return (PlanAccess.None, CardAccess.None);
        }

        var viewer = await GetActiveUserAsync(viewerId, ct).ConfigureAwait(false);
        if (viewer is null)
        {
            return (PlanAccess.Public, CardAccess.None);
        }

        // Plans are public even on invite-only cards; the card itself stays private.
        var cardAccess = await GetCardAccessAsync(viewer.Id, plan.CardId, ct: ct).ConfigureAwait(false);
        var isParticipant = await db.PlanParticipants.AnyAsync(
            p => p.PlanId == planId && p.UserId == viewer.Id && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
        var blocked = await IsBlockedEitherWayAsync(viewer.Id, plan.HostId, ct).ConfigureAwait(false);
        return (AccessRules.PlanAccessFor(plan.HostId == viewer.Id, isParticipant, cardAccess, blocked), cardAccess);
    }

    /// <summary>
    /// Location fields a viewer may see. Distances are measured to the grid-snapped public point,
    /// never to the exact one, unless the viewer is a participant.
    /// </summary>
    public async Task<PlanLocationView?> GetPlanLocationAsync(Guid? viewerId, Guid planId, Point? from, CancellationToken ct = default)
    {
        var (access, _) = await GetPlanAccessAsync(viewerId, planId, ct).ConfigureAwait(false);
        if (access == PlanAccess.None)
        {
            return null;
        }

        var exact = AccessRules.CanSeeExactLocation(access);
        var row = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => new
            {
                p.OriginAreaLabel,
                p.Destination,
                p.DestinationPrecision,
                Origin = exact ? p.Origin : null,
                Meters = from == null ? (double?)null : exact ? p.Origin.Distance(from) : p.OriginPublic.Distance(from),
            })
            .FirstAsync(ct).ConfigureAwait(false);

        return new PlanLocationView(
            row.OriginAreaLabel,
            row.Meters is { } m ? AccessRules.RoundDistance(m) : null,
            row.Origin,
            exact || row.DestinationPrecision == LocationPrecision.Regional ? row.Destination : null);
    }

    public async Task<bool> CanReadConversationAsync(Guid? viewerId, Guid conversationId, CancellationToken ct = default)
    {
        var viewer = await GetActiveUserAsync(viewerId, ct).ConfigureAwait(false);
        if (viewer is null)
        {
            return false;
        }

        var convo = await db.Conversations.AsNoTracking()
            .Where(c => c.Id == conversationId)
            .Select(c => new { c.Type, c.RefId })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (convo is null)
        {
            return false;
        }

        switch (convo.Type)
        {
            case ConversationType.Card:
                var cardAccess = await GetCardAccessAsync(viewer.Id, convo.RefId!.Value, ct: ct).ConfigureAwait(false);
                return AccessRules.CanReadConversation(convo.Type, cardAccess, PlanAccess.None, false, false);
            case ConversationType.Plan:
                var (planAccess, _) = await GetPlanAccessAsync(viewer.Id, convo.RefId!.Value, ct).ConfigureAwait(false);
                return AccessRules.CanReadConversation(convo.Type, CardAccess.None, planAccess, false, false);
            default:
                var members = await db.ConversationMembers.AsNoTracking()
                    .Where(m => m.ConversationId == conversationId)
                    .Select(m => m.UserId)
                    .ToListAsync(ct).ConfigureAwait(false);
                var isMember = members.Contains(viewer.Id);
                var other = members.FirstOrDefault(id => id != viewer.Id);
                var blocked = other != Guid.Empty && await IsBlockedEitherWayAsync(viewer.Id, other, ct).ConfigureAwait(false);
                return AccessRules.CanReadConversation(convo.Type, CardAccess.None, PlanAccess.None, isMember, blocked);
        }
    }

    /// <summary>Messages a participant sees: moderated (hidden) messages and messages from people they blocked are left out.</summary>
    public IQueryable<Message> VisibleMessages(Guid viewerId, Guid conversationId) =>
        db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.HiddenAt == null)
            .Where(m => !db.Blocks.Any(b => b.BlockerId == viewerId && b.BlockedId == m.SenderId));
}
