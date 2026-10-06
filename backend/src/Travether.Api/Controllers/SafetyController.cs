using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Profiles;
using Travether.Api.Safety;

namespace Travether.Api.Controllers;

public sealed record ReportInput(ReportTargetType TargetType, Guid TargetId, ReportReason Reason, [MaxLength(1000)] string? Details);

public sealed record BlockedUserDto(Guid Id, string DisplayName, string? PhotoUrl, DateTimeOffset BlockedAt);

/// <summary>Block and report (PLAN.md §4.8). Blocking takes effect everywhere through AccessQueries.</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class SafetyController(TravetherDbContext db, AccessQueries access, RatingQueries ratings, TimeProvider clock) : ControllerBase
{
    private Guid Me => User.RequireUserId();

    [HttpGet("me/blocks")]
    public async Task<IReadOnlyList<BlockedUserDto>> Blocks(CancellationToken ct)
    {
        var me = Me;
        return await db.Blocks.AsNoTracking()
            .Where(b => b.BlockerId == me)
            .OrderByDescending(b => b.CreatedAt)
            .Join(db.Users, b => b.BlockedId, u => u.Id, (b, u) => new BlockedUserDto(u.Id, u.DeletedAt == null ? u.DisplayName : "", u.DeletedAt == null ? u.PhotoUrl : null, b.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Blocks someone. Idempotent. Open join requests between the two of you lapse.</summary>
    [HttpPost("users/{userId:guid}/block")]
    public async Task<IActionResult> Block(Guid userId, CancellationToken ct)
    {
        var me = Me;
        if (userId == me)
        {
            return ApiError.BadRequest("CannotBlockSelf");
        }

        if (!await db.Users.AnyAsync(u => u.Id == userId, ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        var now = clock.GetUtcNow();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO blocks (blocker_id, blocked_id, created_at) VALUES ({me}, {userId}, {now}) ON CONFLICT DO NOTHING", ct).ConfigureAwait(false);

        // Requests either way to the other person's cards and plans lapse.
        await db.CardRequests
            .Where(r => r.Status == RequestStatus.Requested
                && ((r.UserId == me && r.Card.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active && m.Role != CardRole.Member))
                    || (r.UserId == userId && r.Card.Members.Any(m => m.UserId == me && m.Status == MembershipStatus.Active && m.Role != CardRole.Member))))
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        await db.PlanRequests
            .Where(r => r.Status == RequestStatus.Requested
                && ((r.RequesterId == me && r.Plan.HostId == userId) || (r.RequesterId == userId && r.Plan.HostId == me)))
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("users/{userId:guid}/block")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        var me = Me;
        await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Reports something the reporter can see. One open report per person and target.</summary>
    [HttpPost("reports")]
    [EnableRateLimiting(SafetySetup.ReportsRateLimit)]
    public async Task<IActionResult> Report(ReportInput input, CancellationToken ct)
    {
        var me = Me;
        if (!await CanSeeAsync(me, input.TargetType, input.TargetId, ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        db.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = me,
            TargetType = input.TargetType,
            TargetId = input.TargetId,
            Reason = input.Reason,
            Details = string.IsNullOrWhiteSpace(input.Details) ? null : input.Details.Trim(),
            Status = ReportStatus.Open,
            CreatedAt = clock.GetUtcNow(),
        });
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ApiError.Conflict("AlreadyReported");
        }

        return NoContent();
    }

    /// <summary>Only what the reporter can see can be reported, and never their own things.</summary>
    private async Task<bool> CanSeeAsync(Guid me, ReportTargetType type, Guid id, CancellationToken ct)
    {
        switch (type)
        {
            case ReportTargetType.User:
                return id != me && await access.GetProfileAccessAsync(me, id, ct).ConfigureAwait(false) != ProfileAccess.None;
            case ReportTargetType.Card:
                // Invite-only cards are seen through their link; reporting needs only that the card exists and isn't yours.
                return await db.VacationCards.AnyAsync(c => c.Id == id && c.OwnerId != me, ct).ConfigureAwait(false);
            case ReportTargetType.Plan:
                var (plan, _) = await access.GetPlanAccessAsync(me, id, ct).ConfigureAwait(false);
                return plan is not PlanAccess.None and not PlanAccess.Host;
            case ReportTargetType.Message:
                var message = await db.Messages.AsNoTracking().Where(m => m.Id == id && m.HiddenAt == null).Select(m => new { m.ConversationId, m.SenderId }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                return message is not null && message.SenderId != me && await access.CanReadConversationAsync(me, message.ConversationId, ct).ConfigureAwait(false);
            case ReportTargetType.Review:
                var review = await db.Reviews.AsNoTracking().Where(r => r.Id == id && r.HiddenAt == null).Select(r => new { r.RevieweeId, r.ReviewerId }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                return review is not null && review.ReviewerId != me
                    && (review.RevieweeId == me || await ratings.PublishedAbout(review.RevieweeId).AnyAsync(r => r.Id == id, ct).ConfigureAwait(false));
            default:
                return false;
        }
    }
}
