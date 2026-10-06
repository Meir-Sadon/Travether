using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Places;
using Travether.Api.Plans;

namespace Travether.Api.Notifications;

/// <summary>Scheduled notifications (PLAN.md §4.7): plan reminders and the daily digest of new plans near a trip.</summary>
public sealed partial class NotificationJobs(
    TravetherDbContext db, Notifier notifier, IPlaceSearch places, NotificationOptions options, TimeProvider clock, ILogger<NotificationJobs> log)
{
    public const int DigestRadiusKm = 30;

    /// <summary>Trips starting within this many days get a digest.</summary>
    public const int DigestLeadDays = 14;

    /// <summary>
    /// Reminds participants 24 h and 2 h before a plan. A plan created inside a window skips that
    /// reminder (nobody needs "tomorrow" right after making it); each reminder is sent once.
    /// </summary>
    public async Task<int> RemindAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var sent = 0;
        foreach (var hours in new[] { 24, 2 })
        {
            var from = hours == 24 ? now.AddHours(2) : now;
            var until = now.AddHours(hours);
            var plans = await db.ActivityPlans.AsNoTracking()
                .Where(p => (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full) && p.StartsAt > from && p.StartsAt <= until)
                .Where(p => p.CreatedAt < p.StartsAt.AddHours(-hours))
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    Going = p.Participants.Where(x => x.Status == MembershipStatus.Active).Select(x => x.UserId).ToList(),
                })
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var plan in plans)
            {
                sent += await notifier.SendAsync(
                    plan.Going,
                    NotificationTypes.PlanReminder,
                    new NotificationPayload($"/plans/{plan.Id}", Subject: plan.Title, Count: hours),
                    string.Create(CultureInfo.InvariantCulture, $"reminder-{hours}h:{plan.Id:N}"),
                    ct).ConfigureAwait(false);
            }
        }

        return sent;
    }

    /// <summary>
    /// "Did you meet?" (PLAN.md §4.6): from 10:00 local time the day after a plan it becomes Done and
    /// everyone on it with company is asked; on day 7 those who still haven't answered are asked again.
    /// </summary>
    public async Task<int> MeetPromptsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var since = now.AddDays(-8);
        var plans = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.Status != PlanStatus.Cancelled && p.StartsAt < now && p.StartsAt > since)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.StartsAt,
                p.TimeZoneId,
                p.Status,
                Going = p.Participants.Where(x => x.Status == MembershipStatus.Active).Select(x => x.UserId).ToList(),
                Answered = db.MeetConfirmations.Where(m => m.PlanId == p.Id).Select(m => m.UserId).ToList(),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        var sent = 0;
        foreach (var plan in plans)
        {
            var promptAt = PlanRules.ToInstant(PlanRules.ToLocal(plan.StartsAt, plan.TimeZoneId).Date.AddDays(1), new TimeOnly(10, 0), plan.TimeZoneId);
            if (now < promptAt)
            {
                continue;
            }

            if (plan.Status is PlanStatus.Open or PlanStatus.Full)
            {
                await db.ActivityPlans.Where(p => p.Id == plan.Id && (p.Status == PlanStatus.Open || p.Status == PlanStatus.Full))
                    .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PlanStatus.Done), ct).ConfigureAwait(false);
            }

            var waiting = plan.Going.Except(plan.Answered).ToList();
            if (plan.Going.Count < 2 || waiting.Count == 0)
            {
                continue;
            }

            var payload = new NotificationPayload($"/plans/{plan.Id}/review", Subject: plan.Title);
            sent += await notifier.SendAsync(waiting, NotificationTypes.MeetPrompt, payload, $"meet-1:{plan.Id:N}", ct).ConfigureAwait(false);
            if (now >= promptAt.AddDays(6))
            {
                sent += await notifier.SendAsync(waiting, NotificationTypes.MeetPrompt, payload, $"meet-7:{plan.Id:N}", ct).ConfigureAwait(false);
            }
        }

        return sent;
    }

    /// <summary>
    /// Once a day, from <see cref="NotificationOptions.DigestHour"/> local time, tells members of
    /// current and upcoming trips how many plans near the trip were posted in the last 24 hours,
    /// using the same exclusions as Discover.
    /// </summary>
    public async Task<int> DigestAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var rows = await db.CardMembers.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active && m.Card.EndsOn >= today && m.Card.StartsOn <= today.AddDays(DigestLeadDays))
            .Where(m => m.User.BannedAt == null && m.User.DeletedAt == null)
            .Select(m => new { m.UserId, m.CardId })
            .ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return 0;
        }

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var settings = await db.NotificationSettings.AsNoTracking().Where(s => userIds.Contains(s.UserId)).ToDictionaryAsync(s => s.UserId, ct).ConfigureAwait(false);
        var cards = new Dictionary<Guid, VacationCard?>();
        var sent = 0;

        foreach (var row in rows)
        {
            var s = settings.GetValueOrDefault(row.UserId) ?? new NotificationSettings { UserId = row.UserId };
            var local = NotificationRules.LocalNow(s, now);
            if (!s.Matches || local.Hour < options.DigestHour)
            {
                continue;
            }

            var key = string.Create(CultureInfo.InvariantCulture, $"digest:{row.CardId:N}:{local:yyyy-MM-dd}");
            if (await db.Notifications.AnyAsync(n => n.UserId == row.UserId && n.DedupeKey == key, ct).ConfigureAwait(false))
            {
                continue;
            }

            if (!cards.TryGetValue(row.CardId, out var card))
            {
                card = cards[row.CardId] = await WithAreaAsync(row.CardId, ct).ConfigureAwait(false);
            }

            if (card?.Area is null)
            {
                continue;
            }

            var count = await CountNewPlansAsync(row.UserId, card, now, ct).ConfigureAwait(false);
            if (count > 0)
            {
                sent += await notifier.SendAsync([row.UserId], NotificationTypes.MatchesDigest, new NotificationPayload("/discover", Subject: card.Name, Count: count), key, ct).ConfigureAwait(false);
            }
        }

        return sent;
    }

    private async Task<int> CountNewPlansAsync(Guid me, VacationCard card, DateTimeOffset now, CancellationToken ct)
    {
        var windowStart = new DateTimeOffset(card.StartsOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(-14);
        var windowEnd = new DateTimeOffset(card.EndsOn.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddHours(12);
        var since = now.AddDays(-1);
        var plans = await db.ActivityPlans.AsNoTracking()
            .Where(p => p.Status == PlanStatus.Open && p.CreatedAt > since && p.StartsAt > now && p.StartsAt >= windowStart && p.StartsAt < windowEnd)
            .Where(p => p.Host.BannedAt == null && p.Host.DeletedAt == null)
            .Where(p => p.OriginPublic.IsWithinDistance(card.Area!, DigestRadiusKm * 1000.0))
            .Where(p => !db.CardMembers.Any(m => m.CardId == p.CardId && m.UserId == me && m.Status == MembershipStatus.Active))
            .Where(p => !p.Participants.Any(x => x.UserId == me && x.Status == MembershipStatus.Active))
            .Where(p => !db.Blocks.Any(b => (b.BlockerId == me && b.BlockedId == p.HostId) || (b.BlockerId == p.HostId && b.BlockedId == me)))
            .Select(p => new { p.StartsAt, p.TimeZoneId })
            .ToListAsync(ct).ConfigureAwait(false);
        return plans.Count(p => PlanRules.ToLocal(p.StartsAt, p.TimeZoneId).Date is var d && d >= card.StartsOn && d <= card.EndsOn);
    }

    /// <summary>Cards have regions, not coordinates: the first region is geocoded once and kept.</summary>
    private async Task<VacationCard?> WithAreaAsync(Guid cardId, CancellationToken ct)
    {
        var card = await db.VacationCards.FirstOrDefaultAsync(c => c.Id == cardId, ct).ConfigureAwait(false);
        if (card is null || card.Area is not null || card.Regions.Count == 0)
        {
            return card;
        }

        try
        {
            var found = await places.SearchAsync($"{card.Regions[0]}, {CountryName(card.CountryCode)}", null, null, ct).ConfigureAwait(false);
            if (found.Count > 0)
            {
                card.Area = PlanRules.ToPoint(new LatLng(found[0].Lat, found[0].Lng));
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            LogGeocodeFailed(log, ex, cardId);
        }

        return card;
    }

    private static string CountryName(string code)
    {
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not geocode card {CardId} for the digest")]
    private static partial void LogGeocodeFailed(ILogger logger, Exception ex, Guid cardId);
}
