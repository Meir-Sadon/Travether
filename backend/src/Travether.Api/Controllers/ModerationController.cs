using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Email;
using Travether.Api.Profiles;
using Travether.Api.Safety;

namespace Travether.Api.Controllers;

public enum ModerationAction { Dismiss, Remove, Ban, RemoveAndBan }

/// <summary>What was reported, as moderators see it. <see cref="Author"/> is whoever is responsible for it.</summary>
public sealed record ReportTargetDto(string Text, PersonDto? Author, bool AuthorBanned, bool Removed);

public sealed record ReportAdminDto(
    Guid Id, ReportTargetType TargetType, Guid TargetId, ReportReason Reason, string? Details, ReportStatus Status,
    DateTimeOffset CreatedAt, PersonDto Reporter, ReportTargetDto? Target, int OpenReportsOnTarget, string? ResolutionNote);

/// <summary>A note is required for any action: it's the statement of reasons the affected person receives (EU DSA).</summary>
public sealed record ResolveInput(ModerationAction Action, [MaxLength(1000)] string? Note);

public sealed record BanInput([MaxLength(1000)] string? Note);

/// <summary>Moderation queue and actions (PLAN.md §4.8). Moderators only; everyone else gets 404.</summary>
[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class ModerationController(TravetherDbContext db, BanGuard bans, IEmailSender email, TimeProvider clock) : ControllerBase
{
    public const int PageSize = 100;

    [HttpGet("reports")]
    public async Task<IActionResult> Reports(ReportStatus status = ReportStatus.Open, CancellationToken ct = default)
    {
        if (!await IsModeratorAsync(ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        var query = db.Reports.AsNoTracking().Where(r => r.Status == status);
        query = status == ReportStatus.Open ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.ResolvedAt);
        var rows = await query
            .Take(PageSize)
            .Join(db.Users, r => r.ReporterId, u => u.Id, (r, u) => new { Report = r, Reporter = u })
            .ToListAsync(ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var result = new List<ReportAdminDto>();
        foreach (var row in rows)
        {
            var r = row.Report;
            var target = await TargetAsync(r.TargetType, r.TargetId, ct).ConfigureAwait(false);
            var open = await db.Reports.CountAsync(x => x.TargetType == r.TargetType && x.TargetId == r.TargetId && x.Status == ReportStatus.Open, ct).ConfigureAwait(false);
            result.Add(new ReportAdminDto(
                r.Id, r.TargetType, r.TargetId, r.Reason, r.Details, r.Status, r.CreatedAt, PersonDto.From(row.Reporter, today),
                target is null ? null : new ReportTargetDto(target.Text, target.Author is null ? null : PersonDto.From(target.Author, today), target.Author?.BannedAt is not null, target.Removed),
                open, r.ResolutionNote));
        }

        return Ok(result);
    }

    /// <summary>Resolves a report and every other open report on the same target the same way.</summary>
    [HttpPost("reports/{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolveInput input, CancellationToken ct)
    {
        if (!await IsModeratorAsync(ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
        if (report is null)
        {
            return ApiError.NotFound();
        }

        if (report.Status != ReportStatus.Open)
        {
            return ApiError.Conflict("AlreadyDecided");
        }

        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (input.Action != ModerationAction.Dismiss && note is null)
        {
            return ApiError.BadRequest("NoteRequired");
        }

        var removing = input.Action is ModerationAction.Remove or ModerationAction.RemoveAndBan;
        if (removing && report.TargetType == ReportTargetType.User)
        {
            return ApiError.BadRequest("NothingToRemove");
        }

        var target = await TargetAsync(report.TargetType, report.TargetId, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (removing)
        {
            await RemoveAsync(report.TargetType, report.TargetId, now, ct).ConfigureAwait(false);
        }

        if (input.Action is ModerationAction.Ban or ModerationAction.RemoveAndBan && target?.Author is { } author)
        {
            await bans.BanAsync(author.Id, ct).ConfigureAwait(false);
        }

        var status = input.Action == ModerationAction.Dismiss ? ReportStatus.Dismissed : ReportStatus.Actioned;
        var me = User.RequireUserId();
        await db.Reports
            .Where(r => r.TargetType == report.TargetType && r.TargetId == report.TargetId && r.Status == ReportStatus.Open)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.ResolvedById, me)
                .SetProperty(r => r.ResolvedAt, now)
                .SetProperty(r => r.ResolutionNote, note), ct).ConfigureAwait(false);

        if (status == ReportStatus.Actioned && target?.Author is { } affected)
        {
            await email.SendAsync(StatementOfReasons(affected.Email, report.TargetType, input.Action, report.Reason, note!), ct).ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> Ban(Guid userId, BanInput input, CancellationToken ct)
    {
        if (!await IsModeratorAsync(ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        if (string.IsNullOrWhiteSpace(input.Note))
        {
            return ApiError.BadRequest("NoteRequired");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        if (user is null || userId == User.RequireUserId())
        {
            return ApiError.NotFound();
        }

        await bans.BanAsync(userId, ct).ConfigureAwait(false);
        await email.SendAsync(StatementOfReasons(user.Email, ReportTargetType.User, ModerationAction.Ban, null, input.Note.Trim()), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/unban")]
    public async Task<IActionResult> Unban(Guid userId, CancellationToken ct)
    {
        if (!await IsModeratorAsync(ct).ConfigureAwait(false))
        {
            return ApiError.NotFound();
        }

        await bans.UnbanAsync(userId, ct).ConfigureAwait(false);
        return NoContent();
    }

    private sealed record Target(string Text, User? Author, bool Removed);

    private async Task<Target?> TargetAsync(ReportTargetType type, Guid id, CancellationToken ct)
    {
        switch (type)
        {
            case ReportTargetType.User:
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct).ConfigureAwait(false);
                return user is null ? null : new Target(user.Bio is null ? user.DisplayName : $"{user.DisplayName}: {user.Bio}", user, false);
            case ReportTargetType.Card:
                var card = await db.VacationCards.IgnoreQueryFilters().AsNoTracking().Include(c => c.Owner).FirstOrDefaultAsync(c => c.Id == id, ct).ConfigureAwait(false);
                return card is null ? null : new Target(card.Description is null ? card.Name : $"{card.Name}: {card.Description}", card.Owner, card.DeletedAt is not null);
            case ReportTargetType.Plan:
                var plan = await db.ActivityPlans.IgnoreQueryFilters().AsNoTracking().Include(p => p.Host).FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
                return plan is null ? null : new Target(plan.Purpose is null ? plan.Title : $"{plan.Title}: {plan.Purpose}", plan.Host, plan.DeletedAt is not null);
            case ReportTargetType.Message:
                var message = await db.Messages.AsNoTracking().Include(m => m.Sender).FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
                return message is null ? null : new Target(message.Body, message.Sender, message.HiddenAt is not null);
            case ReportTargetType.Review:
                var review = await db.Reviews.AsNoTracking().Include(r => r.Reviewer).FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
                return review is null ? null : new Target($"{review.Stars}★ {review.Text}".Trim(), review.Reviewer, review.HiddenAt is not null);
            default:
                return null;
        }
    }

    /// <summary>Takes the content down. Cards and plans go the way of a deletion: plans cancelled, open requests expired.</summary>
    private async Task RemoveAsync(ReportTargetType type, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        switch (type)
        {
            case ReportTargetType.Message:
                await db.Messages.Where(m => m.Id == id).ExecuteUpdateAsync(u => u.SetProperty(m => m.HiddenAt, now), ct).ConfigureAwait(false);
                break;
            case ReportTargetType.Review:
                await db.Reviews.Where(r => r.Id == id).ExecuteUpdateAsync(u => u.SetProperty(r => r.HiddenAt, now), ct).ConfigureAwait(false);
                break;
            case ReportTargetType.Plan:
                await db.PlanRequests.Where(r => r.PlanId == id && r.Status == RequestStatus.Requested)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
                await db.ActivityPlans.IgnoreQueryFilters().Where(p => p.Id == id)
                    .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled).SetProperty(p => p.DeletedAt, now), ct).ConfigureAwait(false);
                break;
            case ReportTargetType.Card:
                await db.CardRequests.Where(r => r.CardId == id && r.Status == RequestStatus.Requested)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
                await db.PlanRequests.Where(r => r.Plan.CardId == id && r.Status == RequestStatus.Requested)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
                await db.ActivityPlans.Where(p => p.CardId == id && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
                    .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
                await db.VacationCards.IgnoreQueryFilters().Where(c => c.Id == id)
                    .ExecuteUpdateAsync(u => u.SetProperty(c => c.DeletedAt, now), ct).ConfigureAwait(false);
                break;
            default:
                break;
        }
    }

    private static EmailMessage StatementOfReasons(string to, ReportTargetType type, ModerationAction action, ReportReason? reason, string note)
    {
        var what = type switch
        {
            ReportTargetType.Message => "a chat message you sent",
            ReportTargetType.Review => "a review you wrote",
            ReportTargetType.Plan => "a plan you host",
            ReportTargetType.Card => "a trip you own",
            _ => "your account",
        };
        var done = action switch
        {
            ModerationAction.Remove => $"We removed {what} from Travether.",
            ModerationAction.Ban => "We suspended your Travether account.",
            _ => $"We removed {what} and suspended your Travether account.",
        };
        var why = reason is { } r ? $"\n\nReported for: {EnumText.ToDb(r).Replace('_', ' ')}." : "";
        return new EmailMessage(
            to,
            action == ModerationAction.Remove ? "Content removed from Travether" : "Your Travether account was suspended",
            $"{done}{why}\n\nWhy: {note}\n\nIf you think this is a mistake, reply to this email and a person will look at it again.");
    }

    private async Task<bool> IsModeratorAsync(CancellationToken ct)
    {
        var me = User.RequireUserId();
        return await db.Users.AnyAsync(u => u.Id == me && u.Role == UserRole.Moderator && u.BannedAt == null && u.DeletedAt == null, ct).ConfigureAwait(false);
    }
}
