using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Email;
using Travether.Api.Images;
using Travether.Api.Notifications;
using Travether.Api.Plans;
using Travether.Api.Profiles;

namespace Travether.Api.Privacy;

/// <summary>Consent records, data export, account deletion and retention (PLAN.md §4.9, docs/PRIVACY.md).</summary>
public sealed partial class PrivacyService(
    TravetherDbContext db,
    AuthOptions auth,
    RatingQueries ratings,
    IImageStore images,
    IEmailSender email,
    Notifier notifier,
    TimeProvider clock,
    ILogger<PrivacyService> log)
{
    public async Task<bool> NeedsConsentAsync(Guid userId, CancellationToken ct)
    {
        var accepted = await db.Consents
            .Where(c => c.UserId == userId && c.Version == auth.LegalVersion && c.WithdrawnAt == null && PrivacyRules.Legal.Contains(c.Kind))
            .Select(c => c.Kind).Distinct().CountAsync(ct).ConfigureAwait(false);
        return accepted < PrivacyRules.Legal.Count;
    }

    public async Task<PrivacyDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var history = await db.Consents.AsNoTracking().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.GrantedAt)
            .Select(c => new ConsentRecordDto(c.Kind, c.Version, c.GrantedAt, c.WithdrawnAt))
            .ToListAsync(ct).ConfigureAwait(false);
        bool Active(ConsentKind kind) => history.Any(c => c.Kind == kind && c.WithdrawnAt is null);
        var needs = PrivacyRules.Legal.Any(k => !history.Any(c => c.Kind == k && c.Version == auth.LegalVersion && c.WithdrawnAt is null));
        return new PrivacyDto(auth.LegalVersion, needs, Active(ConsentKind.Analytics), Active(ConsentKind.MarketingEmail), history);
    }

    /// <summary>Records acceptance of the current terms, privacy policy and guidelines. Idempotent.</summary>
    public async Task AcceptLegalAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var accepted = await db.Consents
            .Where(c => c.UserId == userId && c.Version == auth.LegalVersion && c.WithdrawnAt == null)
            .Select(c => c.Kind).ToListAsync(ct).ConfigureAwait(false);
        foreach (var kind in PrivacyRules.Legal.Except(accepted))
        {
            db.Consents.Add(new Consent { Id = Guid.NewGuid(), UserId = userId, Kind = kind, Version = auth.LegalVersion, GrantedAt = now });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Grants or withdraws an optional consent. Withdrawing keeps the record with its end date.</summary>
    public async Task SetOptionalAsync(Guid userId, ConsentKind kind, bool granted, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await db.Consents.Where(c => c.UserId == userId && c.Kind == kind && c.WithdrawnAt == null).ToListAsync(ct).ConfigureAwait(false);
        if (granted && active.Count == 0)
        {
            db.Consents.Add(new Consent { Id = Guid.NewGuid(), UserId = userId, Kind = kind, Version = auth.LegalVersion, GrantedAt = now });
        }
        else if (!granted)
        {
            active.ForEach(c => c.WithdrawnAt = now);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Everything stored about the user, in one JSON document (GDPR art. 15 and 20). Reviews about them are
    /// included once published, so the export never reveals a review before the double-blind window ends.
    /// </summary>
    public async Task<byte[]> ExportAsync(Guid me, JsonSerializerOptions json, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me, ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == me).OrderByDescending(n => n.CreatedAt)
            .Select(n => new { n.Type, n.Payload, n.CreatedAt, n.ReadAt }).ToListAsync(ct).ConfigureAwait(false);

        var export = new
        {
            ExportedAt = clock.GetUtcNow(),
            auth.LegalVersion,
            Account = new
            {
                user.Id,
                user.DisplayName,
                user.FullName,
                user.Email,
                user.Phone,
                user.DateOfBirth,
                user.CountryCode,
                user.PhotoUrl,
                user.Bio,
                user.Languages,
                user.Interests,
                Badges = MeDto.BadgeNames(user.VerificationBadges),
                user.Role,
                HasPassword = user.PasswordHash is not null,
                user.CreatedAt,
            },
            SignInMethods = await db.ExternalLogins.AsNoTracking().Where(e => e.UserId == me)
                .Select(e => new { e.Provider, e.CreatedAt }).ToListAsync(ct).ConfigureAwait(false),
            Devices = await db.UserDevices.AsNoTracking().Where(d => d.UserId == me)
                .Select(d => new { d.LastSeenAt }).ToListAsync(ct).ConfigureAwait(false),
            Consents = await db.Consents.AsNoTracking().Where(c => c.UserId == me).OrderBy(c => c.GrantedAt)
                .Select(c => new { c.Kind, c.Version, c.GrantedAt, c.WithdrawnAt }).ToListAsync(ct).ConfigureAwait(false),
            NotificationSettings = await db.NotificationSettings.AsNoTracking().Where(s => s.UserId == me)
                .Select(s => new { s.Requests, s.Messages, s.Matches, s.Reminders, s.Reviews, s.Email, s.QuietFrom, s.QuietTo, s.TimeZoneId })
                .FirstOrDefaultAsync(ct).ConfigureAwait(false),
            PushDevices = (await db.PushSubscriptions.AsNoTracking().Where(p => p.UserId == me)
                .Select(p => new { p.Endpoint, p.CreatedAt }).ToListAsync(ct).ConfigureAwait(false))
                .Select(p => new { Service = Uri.TryCreate(p.Endpoint, UriKind.Absolute, out var uri) ? uri.Host : "", p.CreatedAt }),
            VacationCards = await db.CardMembers.AsNoTracking().IgnoreQueryFilters().Where(m => m.UserId == me)
                .OrderBy(m => m.JoinedAt)
                .Select(m => new
                {
                    m.CardId,
                    m.Card.Name,
                    m.Card.CountryCode,
                    m.Card.Regions,
                    m.Card.StartsOn,
                    m.Card.EndsOn,
                    m.Card.Description,
                    m.Card.Visibility,
                    m.Role,
                    m.Status,
                    m.JoinedAt,
                    Deleted = m.Card.DeletedAt != null,
                })
                .ToListAsync(ct).ConfigureAwait(false),
            CardRequests = await db.CardRequests.AsNoTracking().IgnoreQueryFilters().Where(r => r.UserId == me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.CardId, CardName = r.Card.Name, r.Message, r.Status, r.CreatedAt, r.DecidedAt })
                .ToListAsync(ct).ConfigureAwait(false),
            PlansHosted = (await db.ActivityPlans.AsNoTracking().IgnoreQueryFilters().Where(p => p.HostId == me).OrderBy(p => p.StartsAt)
                .ToListAsync(ct).ConfigureAwait(false))
                .Select(p => new
                {
                    p.Id,
                    p.CardId,
                    p.Title,
                    p.Category,
                    p.OriginName,
                    p.OriginAreaLabel,
                    MeetingPoint = new { Latitude = p.Origin.Y, Longitude = p.Origin.X },
                    p.Destination,
                    p.StartsAt,
                    p.TimeZoneId,
                    p.Purpose,
                    p.SeatLimit,
                    p.Audience,
                    p.Status,
                    p.CreatedAt,
                }),
            PlansJoined = await db.PlanParticipants.AsNoTracking().IgnoreQueryFilters().Where(p => p.UserId == me && p.Plan.HostId != me)
                .OrderBy(p => p.JoinedAt)
                .Select(p => new { p.PlanId, p.Plan.Title, p.Plan.StartsAt, p.Status, p.JoinedAt })
                .ToListAsync(ct).ConfigureAwait(false),
            PlanRequests = await db.PlanRequests.AsNoTracking().IgnoreQueryFilters().Where(r => r.RequesterId == me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.PlanId, PlanTitle = r.Plan.Title, r.Message, r.Status, r.CreatedAt, r.DecidedAt })
                .ToListAsync(ct).ConfigureAwait(false),
            MessagesSent = await db.Messages.AsNoTracking().Where(m => m.SenderId == me).OrderBy(m => m.CreatedAt)
                .Select(m => new { m.ConversationId, m.Kind, m.Body, m.CreatedAt, Hidden = m.HiddenAt != null })
                .ToListAsync(ct).ConfigureAwait(false),
            MeetAnswers = await db.MeetConfirmations.AsNoTracking().Where(m => m.UserId == me).OrderBy(m => m.AnsweredAt)
                .Select(m => new { m.PlanId, m.Answer, m.AnsweredAt })
                .ToListAsync(ct).ConfigureAwait(false),
            ReviewsWritten = await db.Reviews.AsNoTracking().Where(r => r.ReviewerId == me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.Id, r.PlanId, r.RevieweeId, r.Stars, r.Text, r.CreatedAt, r.ReplyText, r.RepliedAt, Hidden = r.HiddenAt != null })
                .ToListAsync(ct).ConfigureAwait(false),
            ReviewsReceived = await ratings.PublishedAbout(me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.Id, r.PlanId, r.Stars, r.Text, r.CreatedAt, r.ReplyText, r.RepliedAt })
                .ToListAsync(ct).ConfigureAwait(false),
            Notifications = notifications.Select(n => new { n.Type, Payload = JsonSerializer.Deserialize<JsonElement>(n.Payload), n.CreatedAt, n.ReadAt }),
            Blocked = await db.Blocks.AsNoTracking().Where(b => b.BlockerId == me).OrderBy(b => b.CreatedAt)
                .Select(b => new { UserId = b.BlockedId, b.CreatedAt }).ToListAsync(ct).ConfigureAwait(false),
            ReportsMade = await db.Reports.AsNoTracking().Where(r => r.ReporterId == me).OrderBy(r => r.CreatedAt)
                .Select(r => new { r.TargetType, r.TargetId, r.Reason, r.Details, r.Status, r.CreatedAt, r.ResolvedAt })
                .ToListAsync(ct).ConfigureAwait(false),
            Note = $"Generated {today:yyyy-MM-dd}. Other people's names are left out; ids refer to their profiles, cards and plans.",
        };
        return JsonSerializer.SerializeToUtf8Bytes(export, json);
    }

    /// <summary>
    /// Deletes the account (GDPR art. 17). Owned cards pass to a co-admin or member, or are deleted; plans the
    /// user hosts that haven't started are cancelled; memberships end; personal fields are scrubbed and the
    /// sign-in methods, devices, notifications and blocks are removed. Messages in shared chats, reviews
    /// written and reports stay, shown as from a deleted account (see docs/PRIVACY.md).
    /// </summary>
    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct).ConfigureAwait(false);
        var (address, name, photo) = (user.Email, user.DisplayName, user.PhotoUrl);

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        // Plans the user hosts that haven't started are cancelled.
        var cancelled = await db.ActivityPlans
            .Where(p => p.HostId == userId && p.StartsAt > now && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
            .Select(p => p.Id).ToListAsync(ct).ConfigureAwait(false);
        await db.ActivityPlans.Where(p => cancelled.Contains(p.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
        await db.PlanRequests.Where(r => cancelled.Contains(r.PlanId) && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);

        await HandOverCardsAsync(userId, now, ct).ConfigureAwait(false);

        // Seats the user held in upcoming plans free up.
        var joined = await db.PlanParticipants
            .Where(p => p.UserId == userId && p.Status == MembershipStatus.Active && p.Plan.StartsAt > now && p.Plan.HostId != userId)
            .Select(p => p.PlanId).ToListAsync(ct).ConfigureAwait(false);
        await db.CardMembers.Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, MembershipStatus.Left), ct).ConfigureAwait(false);
        await db.PlanParticipants.Where(p => p.UserId == userId && p.Status == MembershipStatus.Active)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, MembershipStatus.Left), ct).ConfigureAwait(false);
        foreach (var planId in joined)
        {
            var plan = await db.ActivityPlans.FromSql($"SELECT * FROM activity_plans WHERE id = {planId} FOR UPDATE").FirstAsync(ct).ConfigureAwait(false);
            var seats = await db.PlanParticipants.CountAsync(p => p.PlanId == planId && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);
            plan.Status = PlanRules.StatusFor(plan.Status, seats, plan.SeatLimit);
        }

        await db.CardRequests.Where(r => r.UserId == userId && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Withdrawn).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
        await db.PlanRequests.Where(r => r.RequesterId == userId && r.Status == RequestStatus.Requested)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Withdrawn).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);

        // Reviews about the user go with the profile they described.
        await db.Reviews.Where(r => r.RevieweeId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.ExternalLogins.Where(e => e.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.PushSubscriptions.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.NotificationSettings.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.UserDevices.Where(d => d.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.Blocks.Where(b => b.BlockerId == userId || b.BlockedId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.ConversationReads.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.LoginCodes.Where(c => c.Email == address).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await db.Consents.Where(c => c.UserId == userId && c.WithdrawnAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.WithdrawnAt, now), ct).ConfigureAwait(false);

        user.DisplayName = "";
        user.FullName = "";
        user.Email = PrivacyRules.DeletedEmail(userId);
        user.Phone = null;
        user.DateOfBirth = DateOnly.MinValue;
        user.CountryCode = PrivacyRules.DeletedCountry;
        user.PhotoUrl = null;
        user.Bio = null;
        user.Languages = [];
        user.Interests = [];
        user.VerificationBadges = VerificationBadges.None;
        user.PasswordHash = null;
        user.SessionVersion++;
        user.DeletedAt = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        foreach (var planId in cancelled)
        {
            await notifier.PlanCancelledAsync(planId, userId, ct).ConfigureAwait(false);
        }

        try
        {
            if (photo is not null)
            {
                await images.DeleteAsync(photo, ct).ConfigureAwait(false);
            }

            await email.SendAsync(new EmailMessage(
                address,
                "Your Travether account was deleted",
                $"Hi {name},\n\nYour Travether account and the personal details on it were deleted as you asked. " +
                "You can sign up again at any time with this email address.\n\nIf you didn't do this, reply to this email."), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            LogCleanupFailed(log, ex, userId);
        }
    }

    /// <summary>
    /// Each card the user owns passes to its longest-standing co-admin, else its longest-standing member.
    /// A card with nobody else on it is deleted, which cancels its open plans and expires its requests.
    /// </summary>
    private async Task HandOverCardsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var owned = await db.VacationCards.Where(c => c.OwnerId == userId).Select(c => c.Id).ToListAsync(ct).ConfigureAwait(false);
        foreach (var cardId in owned)
        {
            var heir = await db.CardMembers
                .Where(m => m.CardId == cardId && m.UserId != userId && m.Status == MembershipStatus.Active)
                .Where(m => m.User.DeletedAt == null && m.User.BannedAt == null)
                .OrderBy(m => m.Role == CardRole.CoAdmin ? 0 : 1).ThenBy(m => m.JoinedAt)
                .Select(m => (Guid?)m.UserId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (heir is { } heirId)
            {
                // One owner per card (unique index): step down before the heir steps up.
                await db.CardMembers.Where(m => m.CardId == cardId && m.UserId == userId)
                    .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, CardRole.Member), ct).ConfigureAwait(false);
                await db.CardMembers.Where(m => m.CardId == cardId && m.UserId == heirId)
                    .ExecuteUpdateAsync(u => u.SetProperty(m => m.Role, CardRole.Owner), ct).ConfigureAwait(false);
                await db.VacationCards.Where(c => c.Id == cardId)
                    .ExecuteUpdateAsync(u => u.SetProperty(c => c.OwnerId, heirId), ct).ConfigureAwait(false);
                continue;
            }

            await db.CardRequests.Where(r => r.CardId == cardId && r.Status == RequestStatus.Requested)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
            await db.PlanRequests.Where(r => r.Plan.CardId == cardId && r.Status == RequestStatus.Requested)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct).ConfigureAwait(false);
            await db.ActivityPlans.Where(p => p.CardId == cardId && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
                .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Cancelled), ct).ConfigureAwait(false);
            await db.VacationCards.Where(c => c.Id == cardId)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.DeletedAt, now), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Removes data past its retention period. Run by the background worker.</summary>
    public async Task<int> ApplyRetentionAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var removed = await db.Notifications.Where(n => n.CreatedAt < now - PrivacyRules.KeepNotifications).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        removed += await db.LoginCodes.Where(c => c.ExpiresAt < now - PrivacyRules.KeepLoginCodes).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        removed += await db.UserDevices
            .Where(d => d.LastSeenAt < now - PrivacyRules.KeepDevices && !db.Users.Any(u => u.Id == d.UserId && u.BannedAt != null))
            .ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return removed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Account {UserId} was deleted but its photo or confirmation email failed")]
    private static partial void LogCleanupFailed(ILogger logger, Exception ex, Guid userId);
}
