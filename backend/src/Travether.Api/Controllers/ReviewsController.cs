using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Profiles;
using Travether.Api.Reviews;

namespace Travether.Api.Controllers;

public sealed record ReviewPageDto(IReadOnlyList<ReviewDto> Items, bool HasMore);

/// <summary>
/// "Did you meet?" and double-blind reviews (PLAN.md §4.6). Only participants of a plan take part;
/// reviewing opens when both people said "Yes, we met" and closes 14 days after the second answer.
/// </summary>
[ApiController]
[Route("api")]
public sealed class ReviewsController(TravetherDbContext db, AccessQueries access, ReviewService reviews, RatingQueries ratings, Notifier notifier, TimeProvider clock)
    : ControllerBase
{
    [Authorize]
    [HttpGet("plans/{planId:guid}/wrap-up")]
    public async Task<IActionResult> WrapUp(Guid planId, CancellationToken ct)
    {
        var me = User.RequireUserId();
        return await RequireParticipantAsync(me, planId, ct).ConfigureAwait(false)
            ?? Ok(await reviews.WrapUpAsync(me, planId, ct).ConfigureAwait(false));
    }

    [Authorize]
    [HttpGet("me/wrap-ups")]
    public async Task<IReadOnlyList<PendingWrapUpDto>> Pending(CancellationToken ct) =>
        await reviews.PendingAsync(User.RequireUserId(), ct).ConfigureAwait(false);

    /// <summary>Answers "Did this happen?" once.</summary>
    [Authorize]
    [HttpPost("plans/{planId:guid}/meet")]
    public async Task<IActionResult> Meet(Guid planId, MeetInput input, CancellationToken ct)
    {
        var me = User.RequireUserId();
        if (await RequireParticipantAsync(me, planId, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        var plan = await db.ActivityPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => new { p.StartsAt, p.Status }).FirstAsync(ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (plan.Status == PlanStatus.Cancelled || now < plan.StartsAt)
        {
            return ApiError.Conflict("PlanNotOver");
        }

        if (now > ReviewRules.AnswerUntil(plan.StartsAt))
        {
            return ApiError.Conflict("AnswerClosed");
        }

        db.MeetConfirmations.Add(new MeetConfirmation { PlanId = planId, UserId = me, Answer = input.Answer, AnsweredAt = now });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("AlreadyAnswered");
        }

        return Ok(await reviews.WrapUpAsync(me, planId, ct).ConfigureAwait(false));
    }

    [Authorize]
    [HttpPost("plans/{planId:guid}/reviews")]
    public async Task<IActionResult> Review(Guid planId, ReviewInput input, CancellationToken ct)
    {
        var me = User.RequireUserId();
        if (await RequireParticipantAsync(me, planId, ct).ConfigureAwait(false) is { } denied)
        {
            return denied;
        }

        if (input.Stars is < 1 or > 5)
        {
            return ApiError.BadRequest("InvalidStars");
        }

        // The reviewee must still be on the plan, present and not blocked either way.
        var wrapUp = await reviews.WrapUpAsync(me, planId, ct).ConfigureAwait(false);
        var person = wrapUp.People.FirstOrDefault(p => p.Person.Id == input.RevieweeId);
        if (person is null)
        {
            return ApiError.NotFound();
        }

        if (person.State != ReviewState.Open)
        {
            return ApiError.Conflict(person.State == ReviewState.Reviewed ? "AlreadyReviewed" : "ReviewNotOpen");
        }

        var now = clock.GetUtcNow();
        db.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            ReviewerId = me,
            RevieweeId = input.RevieweeId,
            Stars = input.Stars,
            Text = string.IsNullOrWhiteSpace(input.Text) ? null : input.Text.Trim(),
            CreatedAt = now,
        });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("AlreadyReviewed");
        }

        // Both sides in: both reviews become visible now.
        await db.Reviews
            .Where(r => r.PlanId == planId && r.PublishedAt == null
                && ((r.ReviewerId == me && r.RevieweeId == input.RevieweeId) || (r.ReviewerId == input.RevieweeId && r.RevieweeId == me)))
            .Where(r => db.Reviews.Count(x => x.PlanId == planId
                && ((x.ReviewerId == me && x.RevieweeId == input.RevieweeId) || (x.ReviewerId == input.RevieweeId && x.RevieweeId == me))) == 2)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.PublishedAt, now), ct).ConfigureAwait(false);

        await notifier.ReviewReceivedAsync(planId, me, input.RevieweeId, ct).ConfigureAwait(false);
        return Ok(await reviews.WrapUpAsync(me, planId, ct).ConfigureAwait(false));
    }

    /// <summary>Published reviews about someone. Same visibility as their profile.</summary>
    [HttpGet("users/{userId:guid}/reviews")]
    public async Task<IActionResult> About(Guid userId, DateTimeOffset? before, CancellationToken ct)
    {
        var viewer = User.GetUserId();
        if (await access.GetProfileAccessAsync(viewer, userId, ct).ConfigureAwait(false) == ProfileAccess.None)
        {
            return ApiError.NotFound();
        }

        var (items, hasMore) = await reviews.AboutAsync(userId, viewer, before, ct).ConfigureAwait(false);
        return Ok(new ReviewPageDto(items, hasMore));
    }

    /// <summary>The person reviewed may answer once, in public, after the review is visible.</summary>
    [Authorize]
    [HttpPost("reviews/{reviewId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid reviewId, ReplyInput input, CancellationToken ct)
    {
        var me = User.RequireUserId();
        var review = await db.Reviews.Include(r => r.Reviewer).FirstOrDefaultAsync(r => r.Id == reviewId && r.RevieweeId == me, ct).ConfigureAwait(false);
        if (review is null)
        {
            return ApiError.NotFound();
        }

        if (!await ratings.PublishedAbout(me).AnyAsync(r => r.Id == reviewId, ct).ConfigureAwait(false))
        {
            return ApiError.Conflict("ReviewNotPublished");
        }

        if (review.ReplyText is not null)
        {
            return ApiError.Conflict("AlreadyReplied");
        }

        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return ApiError.BadRequest("ReplyEmpty");
        }

        review.ReplyText = input.Text.Trim();
        review.RepliedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        var plan = await db.ActivityPlans.IgnoreQueryFilters().Where(p => p.Id == review.PlanId).Select(p => new { p.Title, p.Category }).FirstAsync(ct).ConfigureAwait(false);
        return Ok(ReviewService.ToDto(review, plan.Title, plan.Category, DateOnly.FromDateTime(review.RepliedAt.Value.UtcDateTime)));
    }

    private async Task<IActionResult?> RequireParticipantAsync(Guid me, Guid planId, CancellationToken ct)
    {
        var (planAccess, _) = await access.GetPlanAccessAsync(me, planId, ct).ConfigureAwait(false);
        return planAccess switch
        {
            PlanAccess.None => ApiError.NotFound(),
            < PlanAccess.Participant => ApiError.Forbidden("NotParticipant"),
            _ => null,
        };
    }
}
