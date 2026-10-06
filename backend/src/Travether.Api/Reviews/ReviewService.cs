using Microsoft.EntityFrameworkCore;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Plans;
using Travether.Api.Profiles;

namespace Travether.Api.Reviews;

/// <summary>The rules of "Did you meet?" and reviews (PLAN.md §4.6) that don't need the database.</summary>
public static class ReviewRules
{
    /// <summary>"Did you meet?" can be answered from the plan's start for this many days.</summary>
    public const int AnswerDays = 14;

    public static DateTimeOffset AnswerUntil(DateTimeOffset startsAt) => startsAt.AddDays(AnswerDays);

    /// <summary>
    /// The viewer's side of reviewing one co-participant. While the other person hasn't confirmed,
    /// the state stays <see cref="ReviewState.Waiting"/> whatever they answered, so answers never leak.
    /// </summary>
    public static ReviewState StateFor(MeetConfirmation? mine, MeetConfirmation? theirs, bool reviewed, DateTimeOffset answerUntil, DateTimeOffset now)
    {
        if (reviewed)
        {
            return ReviewState.Reviewed;
        }

        if (AccessRules.ReviewWindowEnd(mine, theirs) is { } end)
        {
            return now <= end ? ReviewState.Open : ReviewState.Closed;
        }

        if (mine is { Answer: not MeetAnswer.Met })
        {
            return ReviewState.Closed;
        }

        return now <= answerUntil ? ReviewState.Waiting : ReviewState.Closed;
    }
}

/// <summary>Loads wrap-ups and reviews. Every caller has already checked that the viewer takes part in the plan.</summary>
public sealed class ReviewService(TravetherDbContext db, RatingQueries ratings, TimeProvider clock)
{
    public const int PageSize = 20;

    public async Task<WrapUpDto> WrapUpAsync(Guid me, Guid planId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var plan = await db.ActivityPlans.AsNoTracking().Where(p => p.Id == planId)
            .Select(p => new { p.Id, p.Title, p.Category, p.StartsAt, p.TimeZoneId, p.Status })
            .FirstAsync(ct).ConfigureAwait(false);
        var others = await db.PlanParticipants.AsNoTracking()
            .Where(p => p.PlanId == planId && p.UserId != me && p.Status == MembershipStatus.Active)
            .Where(p => p.User.BannedAt == null && p.User.DeletedAt == null)
            .Where(p => !db.Blocks.Any(b => (b.BlockerId == me && b.BlockedId == p.UserId) || (b.BlockerId == p.UserId && b.BlockedId == me)))
            .OrderBy(p => p.JoinedAt)
            .Select(p => p.User)
            .ToListAsync(ct).ConfigureAwait(false);
        var answers = await db.MeetConfirmations.AsNoTracking().Where(m => m.PlanId == planId).ToDictionaryAsync(m => m.UserId, ct).ConfigureAwait(false);
        var reviews = await db.Reviews.AsNoTracking().Include(r => r.Reviewer)
            .Where(r => r.PlanId == planId && (r.ReviewerId == me || r.RevieweeId == me))
            .ToListAsync(ct).ConfigureAwait(false);

        var mine = answers.GetValueOrDefault(me);
        var answerUntil = ReviewRules.AnswerUntil(plan.StartsAt);
        var people = others.Select(u =>
        {
            var theirs = answers.GetValueOrDefault(u.Id);
            var myReview = reviews.FirstOrDefault(r => r.ReviewerId == me && r.RevieweeId == u.Id);
            var theirReview = reviews.FirstOrDefault(r => r.ReviewerId == u.Id && r.RevieweeId == me);
            var windowEnd = AccessRules.ReviewWindowEnd(mine, theirs);
            var published = theirReview is { HiddenAt: null } && (theirReview.PublishedAt is not null || AccessRules.IsReviewPublished(myReview is not null, windowEnd, now));
            var state = ReviewRules.StateFor(mine, theirs, myReview is not null, answerUntil, now);
            return new WrapUpPersonDto(
                PersonDto.From(u, today),
                state,
                state == ReviewState.Open ? windowEnd : null,
                myReview is null ? null : ToDto(myReview, plan.Title, plan.Category, today),
                theirReview is not null,
                published ? ToDto(theirReview!, plan.Title, plan.Category, today) : null);
        }).ToList();

        return new WrapUpDto(
            plan.Id, plan.Title, plan.Category, PlanRules.ToLocal(plan.StartsAt, plan.TimeZoneId).Date, mine?.Answer,
            mine is null && plan.Status != PlanStatus.Cancelled && now >= plan.StartsAt && now <= answerUntil, answerUntil, people);
    }

    /// <summary>My finished plans of the last weeks that still need an answer or have reviews I can write.</summary>
    public async Task<IReadOnlyList<PendingWrapUpDto>> PendingAsync(Guid me, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-(ReviewRules.AnswerDays + AccessRules.ReviewWindowDays));
        var planIds = await db.PlanParticipants.AsNoTracking()
            .Where(p => p.UserId == me && p.Status == MembershipStatus.Active)
            .Where(p => p.Plan.StartsAt < now && p.Plan.StartsAt > since && p.Plan.Status != PlanStatus.Cancelled)
            .Where(p => p.Plan.Participants.Any(x => x.UserId != me && x.Status == MembershipStatus.Active))
            .OrderByDescending(p => p.Plan.StartsAt)
            .Select(p => p.PlanId)
            .Take(20)
            .ToListAsync(ct).ConfigureAwait(false);

        var pending = new List<PendingWrapUpDto>();
        foreach (var id in planIds)
        {
            var w = await WrapUpAsync(me, id, ct).ConfigureAwait(false);
            var toReview = w.People.Count(p => p.State == ReviewState.Open);
            if (w.CanAnswer || toReview > 0)
            {
                pending.Add(new PendingWrapUpDto(w.PlanId, w.Title, w.Category, w.LocalDate, w.CanAnswer, toReview));
            }
        }

        return pending;
    }

    /// <summary>Published reviews about a user, newest first, leaving out reviewers the viewer blocked or was blocked by.</summary>
    public async Task<(IReadOnlyList<ReviewDto> Items, bool HasMore)> AboutAsync(Guid userId, Guid? viewerId, DateTimeOffset? before, CancellationToken ct)
    {
        var query = ratings.PublishedAbout(userId);
        if (viewerId is { } v)
        {
            query = query.Where(r => !db.Blocks.Any(b => (b.BlockerId == v && b.BlockedId == r.ReviewerId) || (b.BlockerId == r.ReviewerId && b.BlockedId == v)));
        }

        if (before is { } b)
        {
            query = query.Where(r => r.CreatedAt < b);
        }

        var rows = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(PageSize + 1)
            .Select(r => new
            {
                Review = r,
                r.Reviewer,
                Plan = db.ActivityPlans.IgnoreQueryFilters().Where(p => p.Id == r.PlanId).Select(p => new { p.Title, p.Category }).First(),
            })
            .ToListAsync(ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return (rows.Take(PageSize).Select(x => ToDto(x.Review, x.Plan.Title, x.Plan.Category, today, x.Reviewer)).ToList(), rows.Count > PageSize);
    }

    /// <summary>Caches <c>published_at</c> for reviews whose window has closed. Visibility never depends on it.</summary>
    public async Task<int> PublishDueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var closedBefore = now.AddDays(-AccessRules.ReviewWindowDays);
        return await db.Reviews
            .Where(r => r.PublishedAt == null)
            .Where(r => db.MeetConfirmations
                .Where(m => m.PlanId == r.PlanId && (m.UserId == r.ReviewerId || m.UserId == r.RevieweeId))
                .Max(m => (DateTimeOffset?)m.AnsweredAt) < closedBefore)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.PublishedAt, now), ct).ConfigureAwait(false);
    }

    public static ReviewDto ToDto(Review r, string planTitle, PlanCategory category, DateOnly today, User? reviewer = null)
    {
        var author = reviewer ?? r.Reviewer;
        return new ReviewDto(
            r.Id,
            author is null || author.BannedAt is not null || author.DeletedAt is not null ? null : PersonDto.From(author, today),
            r.Stars, r.Text, r.PlanId, planTitle, category, r.CreatedAt, r.ReplyText, r.RepliedAt);
    }
}
