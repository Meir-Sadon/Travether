using Microsoft.EntityFrameworkCore;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Profiles;

/// <summary>Ratings shown on profiles (PLAN.md §4.6): only published reviews count; the average needs 3.</summary>
public sealed class RatingQueries(TravetherDbContext db, TimeProvider clock)
{
    /// <summary>
    /// Reviews about <paramref name="userId"/> that others may see. Published means: the counterpart review
    /// exists, or the 14-day window after the second "Yes, we met" has closed (computed from the data, so a
    /// late background job can't leak or hide a review). <c>published_at</c> is set by that job as a cache.
    /// </summary>
    public IQueryable<Review> PublishedAbout(Guid userId)
    {
        var closedBefore = clock.GetUtcNow().AddDays(-AccessRules.ReviewWindowDays);
        return db.Reviews.AsNoTracking()
            .Where(r => r.RevieweeId == userId && r.HiddenAt == null)
            .Where(r => r.PublishedAt != null
                || db.Reviews.Any(c => c.PlanId == r.PlanId && c.ReviewerId == r.RevieweeId && c.RevieweeId == r.ReviewerId)
                || db.MeetConfirmations
                    .Where(m => m.PlanId == r.PlanId && (m.UserId == r.ReviewerId || m.UserId == r.RevieweeId))
                    .Max(m => (DateTimeOffset?)m.AnsweredAt) < closedBefore);
    }

    public async Task<RatingSummary> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var stats = await PublishedAbout(userId)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = g.Average(r => (double)r.Stars) })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return stats is null
            ? new RatingSummary(null, 0)
            : new RatingSummary(AccessRules.PublicAverage(stats.Count, stats.Average), stats.Count);
    }
}
