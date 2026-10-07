using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Chat;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Notifications;

/// <summary>
/// Records notifications (PLAN.md §4.7) and nudges the open app over SignalR. Push and email are sent
/// afterwards by <see cref="NotificationDelivery"/>, so a slow push service never slows a request.
/// Call it after the change it reports has been committed.
/// </summary>
public sealed class Notifier(TravetherDbContext db, IHubContext<ChatHub> hub, NotificationSignal signal, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Adds one notification per user; with a <paramref name="dedupeKey"/>, users who already have one with that key are skipped.</summary>
    public async Task<int> SendAsync(IEnumerable<Guid> userIds, string type, NotificationPayload payload, string? dedupeKey, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, Json);
        var now = clock.GetUtcNow();
        var created = new List<string>();
        foreach (var userId in userIds.Distinct())
        {
            var id = Guid.NewGuid();
            var inserted = await db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO notifications (id, user_id, type, payload, created_at, dedupe_key)
                VALUES ({id}, {userId}, {type}, {json}::jsonb, {now}, {dedupeKey})
                ON CONFLICT (user_id, dedupe_key) WHERE dedupe_key IS NOT NULL DO NOTHING
                """, ct).ConfigureAwait(false);
            if (inserted > 0)
            {
                created.Add(userId.ToString());
            }
        }

        if (created.Count > 0)
        {
            if (NotificationTypes.InApp(type))
            {
                await hub.Clients.Users(created).SendAsync("notification", new { type }, ct).ConfigureAwait(false);
            }

            signal.Wake();
        }

        return created.Count;
    }

    public async Task CardRequestedAsync(Guid cardId, Guid requesterId, CancellationToken ct)
    {
        var card = await db.VacationCards.AsNoTracking().Where(c => c.Id == cardId).Select(c => new
        {
            c.Name,
            Admins = c.Members.Where(m => m.Status == MembershipStatus.Active && (m.Role == CardRole.Owner || m.Role == CardRole.CoAdmin)).Select(m => m.UserId).ToList(),
        }).FirstAsync(ct).ConfigureAwait(false);
        await SendAsync(card.Admins, NotificationTypes.CardRequest, new NotificationPayload("/inbox", await NameAsync(requesterId, ct).ConfigureAwait(false), card.Name, ActorId: requesterId), null, ct).ConfigureAwait(false);
    }

    public async Task CardRequestDecidedAsync(Guid cardId, Guid requesterId, bool approved, CancellationToken ct)
    {
        var name = await db.VacationCards.AsNoTracking().Where(c => c.Id == cardId).Select(c => c.Name).FirstAsync(ct).ConfigureAwait(false);
        await SendAsync(
            [requesterId],
            approved ? NotificationTypes.CardRequestApproved : NotificationTypes.CardRequestRejected,
            new NotificationPayload(approved ? $"/trips/{cardId}" : "/inbox", Subject: name),
            null,
            ct).ConfigureAwait(false);
    }

    /// <summary>Tells the people who can decide: the host and the plan's card owner and co-admins.</summary>
    public async Task PlanRequestedAsync(Guid planId, Guid requesterId, CancellationToken ct)
    {
        var plan = await db.ActivityPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => new
        {
            p.Title,
            p.HostId,
            Admins = p.Card.Members.Where(m => m.Status == MembershipStatus.Active && (m.Role == CardRole.Owner || m.Role == CardRole.CoAdmin)).Select(m => m.UserId).ToList(),
        }).FirstAsync(ct).ConfigureAwait(false);
        await SendAsync(
            [plan.HostId, .. plan.Admins.Where(id => id != requesterId)],
            NotificationTypes.PlanRequest,
            new NotificationPayload("/inbox", await NameAsync(requesterId, ct).ConfigureAwait(false), plan.Title, ActorId: requesterId),
            null,
            ct).ConfigureAwait(false);
    }

    public async Task PlanRequestDecidedAsync(Guid planId, IEnumerable<Guid> people, bool approved, CancellationToken ct)
    {
        var title = await db.ActivityPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Title).FirstAsync(ct).ConfigureAwait(false);
        await SendAsync(
            people,
            approved ? NotificationTypes.PlanRequestApproved : NotificationTypes.PlanRequestRejected,
            new NotificationPayload($"/plans/{planId}", Subject: title),
            null,
            ct).ConfigureAwait(false);
    }

    /// <summary>A card member took a free seat: the host hears about it.</summary>
    public async Task PlanJoinedAsync(Guid planId, Guid joinerId, CancellationToken ct)
    {
        var plan = await db.ActivityPlans.AsNoTracking().Where(p => p.Id == planId).Select(p => new { p.Title, p.HostId }).FirstAsync(ct).ConfigureAwait(false);
        if (plan.HostId != joinerId)
        {
            await SendAsync([plan.HostId], NotificationTypes.PlanJoined, new NotificationPayload($"/plans/{planId}", await NameAsync(joinerId, ct).ConfigureAwait(false), plan.Title, ActorId: joinerId), null, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Everyone going, except whoever cancelled (PLAN.md §4.6.6).</summary>
    public async Task PlanCancelledAsync(Guid planId, Guid cancelledBy, CancellationToken ct)
    {
        var plan = await db.ActivityPlans.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == planId).Select(p => new
        {
            p.Title,
            Going = p.Participants.Where(x => x.Status == MembershipStatus.Active).Select(x => x.UserId).ToList(),
        }).FirstAsync(ct).ConfigureAwait(false);
        await SendAsync(plan.Going.Where(id => id != cancelledBy), NotificationTypes.PlanCancelled, new NotificationPayload($"/plans/{planId}", Subject: plan.Title), null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Chat pushes are batched: at most one per conversation per ten minutes for each recipient, and
    /// the device replaces the previous one (same tag).
    /// </summary>
    public async Task ChatMessageAsync(Conversation convo, string title, MessageDto message, IEnumerable<Guid> recipients, CancellationToken ct)
    {
        var bucket = clock.GetUtcNow().ToUnixTimeSeconds() / 600;
        var preview = message.Kind == MessageKind.Text ? (message.Body.Length <= 120 ? message.Body : string.Concat(message.Body.AsSpan(0, 119), "…")) : null;
        await SendAsync(
            recipients,
            NotificationTypes.ChatMessage,
            new NotificationPayload($"/inbox/{ChatKeys.For(convo.Type, convo.RefId!.Value)}", message.Sender.DisplayName, title, preview, ActorId: message.Sender.Id),
            $"chat:{convo.Id:N}:{bucket}",
            ct).ConfigureAwait(false);
    }

    /// <summary>Says who reviewed you, never what they wrote (that stays hidden until it's published).</summary>
    public async Task ReviewReceivedAsync(Guid planId, Guid reviewerId, Guid revieweeId, CancellationToken ct) =>
        await SendAsync(
            [revieweeId],
            NotificationTypes.ReviewReceived,
            new NotificationPayload($"/plans/{planId}/review", await NameAsync(reviewerId, ct).ConfigureAwait(false), ActorId: reviewerId),
            $"review:{planId:N}:{reviewerId:N}",
            ct).ConfigureAwait(false);

    private Task<string> NameAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstAsync(ct);
}

/// <summary>Wakes the delivery loop as soon as something is queued, instead of waiting for its next poll.</summary>
public sealed class NotificationSignal : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(0, 1);

    public void Wake()
    {
        if (semaphore.CurrentCount == 0)
        {
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signalled.
            }
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => semaphore.WaitAsync(timeout, ct);

    public void Dispose() => semaphore.Dispose();
}
