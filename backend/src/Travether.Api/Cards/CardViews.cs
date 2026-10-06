using Microsoft.EntityFrameworkCore;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Cards;

/// <summary>Builds <see cref="CardDto"/> for a viewer at a given access level.</summary>
public sealed class CardViews(TravetherDbContext db, TimeProvider clock)
{
    public async Task<CardDto> LoadAsync(Guid id, CardAccess level, Guid? viewerId, CancellationToken ct)
    {
        var card = await db.VacationCards.AsNoTracking().FirstAsync(c => c.Id == id, ct).ConfigureAwait(false);
        var members = await db.CardMembers.AsNoTracking()
            .Where(m => m.CardId == id && m.Status == MembershipStatus.Active && m.User.BannedAt == null)
            .Select(m => new { m.User, m.Role, m.JoinedAt })
            .ToListAsync(ct).ConfigureAwait(false);
        members = [.. members.OrderBy(m => m.Role).ThenBy(m => m.JoinedAt)]; // owner, co-admins, members

        var inside = AccessRules.CanSeeCardInside(level);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var myRequest = level == CardAccess.Preview && viewerId is not null
            ? await db.CardRequests.AsNoTracking()
                .Where(r => r.CardId == id && r.UserId == viewerId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new MyCardRequestDto(r.Id, r.Status, r.CreatedAt))
                .FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : null;
        int? pending = AccessRules.CanDecideCardRequests(level)
            ? await db.CardRequests.CountAsync(r => r.CardId == id && r.Status == RequestStatus.Requested, ct).ConfigureAwait(false)
            : null;
        return new CardDto(
            card.Id, card.Name, card.CountryCode, card.Regions, card.StartsOn, card.EndsOn, card.Description, card.CoverUrl,
            card.Visibility, members.Count, CardRules.AccessName(level),
            inside ? card.ShareSlug : null,
            inside ? members.Select(m => new CardMemberDto(PersonDto.From(m.User, today), m.Role, m.JoinedAt)).ToList() : null,
            myRequest,
            pending);
    }
}
