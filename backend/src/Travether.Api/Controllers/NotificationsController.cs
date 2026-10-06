using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Api;
using Travether.Api.Auth;
using Travether.Api.Data;
using Travether.Api.Domain;
using Travether.Api.Notifications;

namespace Travether.Api.Controllers;

public sealed record NotificationDto(Guid Id, string Type, NotificationPayload Payload, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);

public sealed record NotificationPageDto(IReadOnlyList<NotificationDto> Items, int Unread, bool HasMore);

public sealed record UnreadDto(int Count);

/// <summary>Null marks everything read.</summary>
public sealed record MarkReadInput(IReadOnlyList<Guid>? Ids);

public sealed record NotificationSettingsDto(
    bool Requests, bool Messages, bool Matches, bool Reminders, bool Reviews, bool Email,
    TimeOnly? QuietFrom, TimeOnly? QuietTo, [MaxLength(64)] string TimeZone);

public sealed record PushKeyDto(bool Enabled, string? PublicKey);

public sealed record PushSubscriptionInput([MaxLength(1000)] string Endpoint, [MaxLength(200)] string P256dh, [MaxLength(100)] string Auth, [MaxLength(64)] string? TimeZone);

public sealed record PushUnsubscribeInput([MaxLength(1000)] string Endpoint);

/// <summary>In-app notifications, notification settings and Web Push subscriptions (PLAN.md §4.7).</summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class NotificationsController(TravetherDbContext db, IWebPushSender push, TimeProvider clock) : ControllerBase
{
    public const int PageSize = 30;

    /// <summary>A user keeps at most this many devices subscribed; the oldest drops off.</summary>
    public const int MaxDevices = 10;

    private Guid Me => User.RequireUserId();

    [HttpGet("notifications")]
    public async Task<NotificationPageDto> List(DateTimeOffset? before, CancellationToken ct)
    {
        var mine = Mine();
        var query = before is { } b ? mine.Where(n => n.CreatedAt < b) : mine;
        var rows = await query.OrderByDescending(n => n.CreatedAt).Take(PageSize + 1).ToListAsync(ct).ConfigureAwait(false);
        var unread = await mine.CountAsync(n => n.ReadAt == null, ct).ConfigureAwait(false);
        return new NotificationPageDto(
            rows.Take(PageSize).Select(n => new NotificationDto(n.Id, n.Type, JsonSerializer.Deserialize<NotificationPayload>(n.Payload, Notifier.Json)!, n.ReadAt, n.CreatedAt)).ToList(),
            unread,
            rows.Count > PageSize);
    }

    [HttpGet("notifications/unread")]
    public async Task<UnreadDto> Unread(CancellationToken ct) => new(await Mine().CountAsync(n => n.ReadAt == null, ct).ConfigureAwait(false));

    [HttpPost("notifications/read")]
    public async Task<IActionResult> MarkRead(MarkReadInput input, CancellationToken ct)
    {
        var query = Mine().Where(n => n.ReadAt == null);
        if (input.Ids is { } ids)
        {
            query = query.Where(n => ids.Contains(n.Id));
        }

        var now = clock.GetUtcNow();
        await query.ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("notifications/settings")]
    public async Task<NotificationSettingsDto> GetSettings(CancellationToken ct) => ToDto(await LoadSettingsAsync(ct).ConfigureAwait(false));

    [HttpPut("notifications/settings")]
    public async Task<IActionResult> SaveSettings(NotificationSettingsDto input, CancellationToken ct)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(input.TimeZone, out _))
        {
            return ApiError.BadRequest("InvalidTimeZone");
        }

        if ((input.QuietFrom is null) != (input.QuietTo is null))
        {
            return ApiError.BadRequest("InvalidQuietHours");
        }

        var s = await LoadSettingsAsync(ct).ConfigureAwait(false);
        s.Requests = input.Requests;
        s.Messages = input.Messages;
        s.Matches = input.Matches;
        s.Reminders = input.Reminders;
        s.Reviews = input.Reviews;
        s.Email = input.Email;
        s.QuietFrom = input.QuietFrom;
        s.QuietTo = input.QuietTo;
        s.TimeZoneId = input.TimeZone;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Ok(ToDto(s));
    }

    /// <summary>The VAPID public key the browser subscribes with. Open to visitors so the app can check support early.</summary>
    [AllowAnonymous]
    [HttpGet("push/key")]
    public PushKeyDto Key() => new(push.Enabled, push.Enabled ? push.PublicKey : null);

    /// <summary>Saves this device's push subscription (moving it to the current user if another account had it).</summary>
    [HttpPost("push/subscriptions")]
    public async Task<IActionResult> Subscribe(PushSubscriptionInput input, CancellationToken ct)
    {
        if (!PushEndpoints.IsAllowed(input.Endpoint))
        {
            return ApiError.BadRequest("PushEndpointNotAllowed");
        }

        if (!IsKey(input.P256dh, 65) || !IsKey(input.Auth, 16))
        {
            return ApiError.BadRequest("InvalidPushKeys");
        }

        var me = Me;
        var sub = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == input.Endpoint, ct).ConfigureAwait(false);
        if (sub is null)
        {
            db.PushSubscriptions.Add(new PushSubscription { Id = Guid.NewGuid(), UserId = me, Endpoint = input.Endpoint, P256dh = input.P256dh, Auth = input.Auth, CreatedAt = clock.GetUtcNow() });
        }
        else
        {
            sub.UserId = me;
            sub.P256dh = input.P256dh;
            sub.Auth = input.Auth;
            sub.CreatedAt = clock.GetUtcNow();
        }

        if (input.TimeZone is { } zone && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _))
        {
            (await LoadSettingsAsync(ct).ConfigureAwait(false)).TimeZoneId = zone;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var extra = await db.PushSubscriptions.Where(s => s.UserId == me).OrderByDescending(s => s.CreatedAt).Skip(MaxDevices).Select(s => s.Id).ToListAsync(ct).ConfigureAwait(false);
        if (extra.Count > 0)
        {
            await db.PushSubscriptions.Where(s => extra.Contains(s.Id)).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }

        return NoContent();
    }

    [HttpDelete("push/subscriptions")]
    public async Task<IActionResult> Unsubscribe(PushUnsubscribeInput input, CancellationToken ct)
    {
        var me = Me;
        await db.PushSubscriptions.Where(s => s.UserId == me && s.Endpoint == input.Endpoint).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return NoContent();
    }

    private IQueryable<Notification> Mine()
    {
        var me = Me;
        return db.Notifications.Where(n => n.UserId == me && n.Type != NotificationTypes.ChatMessage);
    }

    /// <summary>The user's settings row, created with the defaults (and tracked) when missing.</summary>
    private async Task<NotificationSettings> LoadSettingsAsync(CancellationToken ct)
    {
        var me = Me;
        var s = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == me, ct).ConfigureAwait(false);
        if (s is null)
        {
            s = new NotificationSettings { UserId = me };
            db.NotificationSettings.Add(s);
        }

        return s;
    }

    private static NotificationSettingsDto ToDto(NotificationSettings s) =>
        new(s.Requests, s.Messages, s.Matches, s.Reminders, s.Reviews, s.Email, s.QuietFrom, s.QuietTo, s.TimeZoneId);

    private static bool IsKey(string value, int length)
    {
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(value);
            return bytes.Length == length && (length != 65 || bytes[0] == 0x04);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
