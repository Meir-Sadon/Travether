using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Cards;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Images;
using Travether.Api.Profiles;

namespace Travether.Api.Controllers;

/// <summary>Vacation Cards (PLAN.md §4.2). Every read and write is gated by AccessQueries/AccessRules.</summary>
[ApiController]
[Route("api/cards")]
public sealed class CardsController(TravetherDbContext db, AccessQueries access, CardViews views, IImageStore images, TimeProvider clock) : ControllerBase
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>The signed-in user's active trips, current and upcoming first.</summary>
    [Authorize]
    [HttpGet]
    public async Task<IReadOnlyList<MyCardDto>> Mine(CancellationToken ct)
    {
        var userId = User.RequireUserId();
        var today = Today;
        var rows = await db.CardMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Select(m => new
            {
                m.Card,
                m.Role,
                MemberCount = m.Card.Members.Count(x => x.Status == MembershipStatus.Active),
                PlanCount = m.Card.Plans.Count(p => p.Status != PlanStatus.Cancelled),
                Pending = m.Role == CardRole.Member ? 0 : db.CardRequests.Count(r => r.CardId == m.CardId && r.Status == RequestStatus.Requested),
                Preview = m.Card.Members
                    .Where(x => x.Status == MembershipStatus.Active && x.User.BannedAt == null)
                    .OrderBy(x => x.JoinedAt)
                    .Select(x => x.User)
                    .Take(5)
                    .ToList(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .OrderBy(r => r.Card.EndsOn < today) // finished trips last
            .ThenBy(r => r.Card.StartsOn)
            .Select(r => new MyCardDto(
                r.Card.Id, r.Card.Name, r.Card.CountryCode, r.Card.Regions, r.Card.StartsOn, r.Card.EndsOn, r.Card.CoverUrl,
                r.Card.Visibility, r.Role, r.MemberCount, r.PlanCount, r.Preview.Select(u => PersonDto.From(u, today)).ToList(), r.Pending))
            .ToList();
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(CardInput input, CancellationToken ct)
    {
        var userId = User.RequireUserId();
        var regions = CardRules.NormalizeRegions(input.Regions);
        if (CardRules.Validate(input.Name, regions, input.StartsOn, input.EndsOn, Today, isNew: true) is { } error)
        {
            return ApiError.BadRequest(error);
        }

        var card = new VacationCard
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            Name = input.Name.Trim(),
            CountryCode = input.CountryCode,
            Regions = regions,
            StartsOn = input.StartsOn,
            EndsOn = input.EndsOn,
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            Visibility = input.Visibility,
            ShareSlug = CardRules.NewSlug(),
            CreatedAt = clock.GetUtcNow(),
            Members = [new CardMember { UserId = userId, Role = CardRole.Owner, Status = MembershipStatus.Active, JoinedAt = clock.GetUtcNow() }],
        };
        db.VacationCards.Add(card);
        db.Conversations.Add(new Conversation { Id = Guid.NewGuid(), Type = ConversationType.Card, RefId = card.Id, CreatedAt = card.CreatedAt });

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A share-slug collision (~1 in 10^17); asking the client to retry is enough.
            return ApiError.Conflict("TryAgain");
        }

        return CreatedAtAction(nameof(Get), new { id = card.Id }, await LoadAsync(card.Id, CardAccess.Owner, ct).ConfigureAwait(false));
    }

    /// <summary>A card by id: the preview for public cards, the inside for members.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(User.GetUserId(), id, ct: ct).ConfigureAwait(false);
        return level == CardAccess.None ? ApiError.NotFound() : Ok(await LoadAsync(id, level, ct).ConfigureAwait(false));
    }

    /// <summary>A card opened from its share link or QR code; works for invite-only cards too.</summary>
    [HttpGet("share/{slug}")]
    public async Task<IActionResult> GetByShareSlug(string slug, CancellationToken ct)
    {
        var id = await db.VacationCards.AsNoTracking().Where(c => c.ShareSlug == slug).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (id is null)
        {
            return ApiError.NotFound();
        }

        var level = await access.GetCardAccessAsync(User.GetUserId(), id.Value, slug, ct).ConfigureAwait(false);
        return level == CardAccess.None ? ApiError.NotFound() : Ok(await LoadAsync(id.Value, level, ct).ConfigureAwait(false));
    }

    [Authorize]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CardPatch patch, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(User.GetUserId(), id, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var card = await db.VacationCards.FirstAsync(c => c.Id == id, ct).ConfigureAwait(false);
        var regions = patch.Regions is null ? card.Regions : CardRules.NormalizeRegions(patch.Regions);
        var name = patch.Name ?? card.Name;
        var startsOn = patch.StartsOn ?? card.StartsOn;
        var endsOn = patch.EndsOn ?? card.EndsOn;
        if (CardRules.Validate(name, regions, startsOn, endsOn, Today, isNew: false) is { } error)
        {
            return ApiError.BadRequest(error);
        }

        card.Name = name.Trim();
        card.Regions = regions;
        card.StartsOn = startsOn;
        card.EndsOn = endsOn;
        card.CountryCode = patch.CountryCode ?? card.CountryCode;
        card.Visibility = patch.Visibility ?? card.Visibility;
        if (patch.Description is not null)
        {
            card.Description = string.IsNullOrWhiteSpace(patch.Description) ? null : patch.Description.Trim();
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(await LoadAsync(id, level, ct).ConfigureAwait(false));
    }

    /// <summary>
    /// Deletes the card (soft delete). Its plans disappear with it and open join requests expire.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(User.GetUserId(), id, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.CardRequests.Where(r => r.CardId == id && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        await db.PlanRequests.Where(r => r.Plan.CardId == id && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        await db.ActivityPlans.Where(p => p.CardId == id && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
        await db.VacationCards.Where(c => c.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.DeletedAt, now), ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Issues a new share link; the old link and QR code stop working.</summary>
    [Authorize]
    [HttpPost("{id:guid}/share-link")]
    public async Task<IActionResult> RotateShareLink(Guid id, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(User.GetUserId(), id, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        await db.VacationCards.Where(c => c.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.ShareSlug, CardRules.NewSlug()), ct).ConfigureAwait(false);
        return Ok(await LoadAsync(id, level, ct).ConfigureAwait(false));
    }

    [Authorize]
    [HttpPost("{id:guid}/cover")]
    [RequestSizeLimit(ImageRules.MaxBytes + 64 * 1024)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file, CancellationToken ct)
    {
        var level = await access.GetCardAccessAsync(User.GetUserId(), id, ct: ct).ConfigureAwait(false);
        if (!AccessRules.CanManageCard(level))
        {
            return level == CardAccess.None ? ApiError.NotFound() : ApiError.Forbidden();
        }

        var (data, kind, error) = await ImageRules.ReadAsync(file, ct).ConfigureAwait(false);
        if (data is null)
        {
            return ApiError.BadRequest(error!);
        }

        string url;
        await using (data)
        {
            url = await images.SaveAsync(data, kind, "covers", ct).ConfigureAwait(false);
        }

        var card = await db.VacationCards.FirstAsync(c => c.Id == id, ct).ConfigureAwait(false);
        var old = card.CoverUrl;
        card.CoverUrl = url;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        if (old is not null)
        {
            await images.DeleteAsync(old, ct).ConfigureAwait(false);
        }

        return Ok(await LoadAsync(id, level, ct).ConfigureAwait(false));
    }

    private Task<CardDto> LoadAsync(Guid id, CardAccess level, CancellationToken ct) => views.LoadAsync(id, level, User.GetUserId(), ct);
}
