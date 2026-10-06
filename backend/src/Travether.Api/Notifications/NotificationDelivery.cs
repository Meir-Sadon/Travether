using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Email;

namespace Travether.Api.Notifications;

public sealed class NotificationOptions
{
    /// <summary>Where the app lives, for links in emails.</summary>
    public string PublicUrl { get; set; } = "http://localhost:5173";

    /// <summary>Runs delivery, reminders and the digest in the background. Tests turn it off and call the jobs directly.</summary>
    public bool RunJobs { get; set; } = true;

    /// <summary>Local hour from which the daily digest goes out.</summary>
    public int DigestHour { get; set; } = 8;
}

/// <summary>
/// Sends queued notifications by push and email according to each user's settings: a category that
/// is off sends nothing, quiet hours hold back push only, and email goes only for the types that need it.
/// </summary>
public sealed partial class NotificationDelivery(
    TravetherDbContext db, IWebPushSender push, IEmailSender email, NotificationOptions options, TimeProvider clock, ILogger<NotificationDelivery> log)
{
    public const int BatchSize = 200;

    /// <summary>Queued notifications older than this are marked done without sending: the moment has passed.</summary>
    private static readonly TimeSpan Stale = TimeSpan.FromHours(12);

    public async Task<int> DeliverPendingAsync(CancellationToken ct)
    {
        var batch = await db.Notifications.Where(n => n.DeliveredAt == null).OrderBy(n => n.CreatedAt).Take(BatchSize).ToListAsync(ct).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow();
        var userIds = batch.Select(n => n.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.BannedAt == null && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct).ConfigureAwait(false);
        var settings = await db.NotificationSettings.AsNoTracking().Where(s => userIds.Contains(s.UserId)).ToDictionaryAsync(s => s.UserId, ct).ConfigureAwait(false);
        var subscriptions = (await db.PushSubscriptions.Where(s => userIds.Contains(s.UserId)).ToListAsync(ct).ConfigureAwait(false)).ToLookup(s => s.UserId);
        var gone = new HashSet<Guid>();

        foreach (var n in batch)
        {
            n.DeliveredAt = now;
            var s = settings.GetValueOrDefault(n.UserId) ?? new NotificationSettings { UserId = n.UserId };
            if (!users.TryGetValue(n.UserId, out var address) || now - n.CreatedAt > Stale || !NotificationRules.Wants(s, NotificationTypes.CategoryOf(n.Type)))
            {
                continue;
            }

            var payload = JsonSerializer.Deserialize<NotificationPayload>(n.Payload, Notifier.Json)!;
            var (title, body) = NotificationTypes.Text(n.Type, payload);

            if (push.Enabled && !NotificationRules.InQuietHours(s, now))
            {
                var message = new PushMessage(title, body, payload.Url, $"{n.Type}:{payload.Url}");
                foreach (var sub in subscriptions[n.UserId].Where(x => !gone.Contains(x.Id)))
                {
                    if (await push.SendAsync(sub, message, ct).ConfigureAwait(false) == PushResult.Gone)
                    {
                        gone.Add(sub.Id);
                        db.PushSubscriptions.Remove(sub);
                    }
                }
            }

            if (s.Email && NotificationTypes.Emails(n.Type))
            {
                var link = options.PublicUrl.TrimEnd('/') + payload.Url;
                var text = $"{body}\n\nOpen Travether: {link}\n\nChoose what we send you: {options.PublicUrl.TrimEnd('/')}/settings";
                try
                {
                    await email.SendAsync(new EmailMessage(address, title, text), ct).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    LogEmailFailed(log, ex, n.Id);
                }
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return batch.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification email {NotificationId} failed")]
    private static partial void LogEmailFailed(ILogger logger, Exception ex, Guid notificationId);
}
