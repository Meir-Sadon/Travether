using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Authorization;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Chat;

/// <summary>Card and plan chats (PLAN.md §4.3, decision 3): who may read, who gets pushed a message.</summary>
public sealed class ChatService(TravetherDbContext db, AccessQueries access, IHubContext<ChatHub> hub, TimeProvider clock)
{
    public const int PageSize = 50;

    /// <summary>Unread counts start from here for people who never opened a chat.</summary>
    private static readonly DateTimeOffset Epoch = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The conversation of a card or plan, if the viewer may read it.</summary>
    public async Task<Conversation?> OpenAsync(Guid viewerId, ConversationType type, Guid refId, CancellationToken ct)
    {
        var convo = await db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Type == type && c.RefId == refId, ct).ConfigureAwait(false);
        return convo is not null && await access.CanReadConversationAsync(viewerId, convo.Id, ct).ConfigureAwait(false) ? convo : null;
    }

    public async Task<MessageDto> PostAsync(Conversation convo, Guid senderId, string body, MessageKind kind, CancellationToken ct)
    {
        var message = new Message { Id = Guid.NewGuid(), ConversationId = convo.Id, SenderId = senderId, Body = body, Kind = kind, CreatedAt = clock.GetUtcNow() };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await MarkReadAsync(convo.Id, senderId, message.CreatedAt, ct).ConfigureAwait(false);

        var sender = await db.Users.AsNoTracking().Where(u => u.Id == senderId).Select(u => new SenderDto(u.Id, u.DisplayName, u.PhotoUrl)).FirstAsync(ct).ConfigureAwait(false);
        var dto = new MessageDto(message.Id, sender, message.Body, message.Kind, message.CreatedAt);
        var recipients = await RecipientsAsync(convo, senderId, ct).ConfigureAwait(false);
        await hub.Clients.Users(recipients.Select(id => id.ToString())).SendAsync("message", new MessageEvent(ChatKeys.For(convo.Type, convo.RefId!.Value), dto), ct).ConfigureAwait(false);
        return dto;
    }

    /// <summary>Messages before a point in time, newest page first, returned oldest first.</summary>
    public async Task<(IReadOnlyList<MessageDto> Messages, bool HasMore)> PageAsync(Guid viewerId, Guid conversationId, DateTimeOffset? before, CancellationToken ct)
    {
        var query = access.VisibleMessages(viewerId, conversationId);
        if (before is { } b)
        {
            query = query.Where(m => m.CreatedAt < b);
        }

        var page = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(PageSize + 1)
            .Select(m => new MessageDto(m.Id, new SenderDto(m.Sender.Id, m.Sender.DisplayName, m.Sender.PhotoUrl), m.Body, m.Kind, m.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);
        var hasMore = page.Count > PageSize;
        return ([.. page.Take(PageSize).Reverse()], hasMore);
    }

    public async Task MarkReadAsync(Guid conversationId, Guid userId, DateTimeOffset at, CancellationToken ct) =>
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO conversation_reads (conversation_id, user_id, last_read_at) VALUES ({conversationId}, {userId}, {at})
            ON CONFLICT (conversation_id, user_id) DO UPDATE SET last_read_at = GREATEST(conversation_reads.last_read_at, EXCLUDED.last_read_at)
            """, ct).ConfigureAwait(false);

    /// <summary>The viewer's card and plan chats, most recent activity first.</summary>
    public async Task<IReadOnlyList<ChatSummaryDto>> ListAsync(Guid me, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        var cards = await db.CardMembers.AsNoTracking()
            .Where(m => m.UserId == me && m.Status == MembershipStatus.Active)
            .Select(m => new { RefId = m.CardId, Title = m.Card.Name, Category = (PlanCategory?)null })
            .ToListAsync(ct).ConfigureAwait(false);
        var plans = await db.PlanParticipants.AsNoTracking()
            .Where(p => p.UserId == me && p.Status == MembershipStatus.Active && p.Plan.StartsAt > since)
            .Select(p => new { RefId = p.PlanId, Title = p.Plan.Title, Category = (PlanCategory?)p.Plan.Category })
            .ToListAsync(ct).ConfigureAwait(false);
        var titles = cards.Concat(plans).ToDictionary(x => x.RefId);
        var refIds = titles.Keys.ToList();

        var rows = await db.Conversations.AsNoTracking()
            .Where(c => c.RefId != null && refIds.Contains(c.RefId.Value) && c.Type != ConversationType.Direct)
            .Select(c => new
            {
                c.Type,
                RefId = c.RefId!.Value,
                c.CreatedAt,
                Last = db.Messages
                    .Where(m => m.ConversationId == c.Id && m.HiddenAt == null && !db.Blocks.Any(b => b.BlockerId == me && b.BlockedId == m.SenderId))
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new { m.Sender.DisplayName, m.SenderId, m.Body, m.Kind, m.CreatedAt })
                    .FirstOrDefault(),
                Unread = db.Messages.Count(m => m.ConversationId == c.Id && m.HiddenAt == null && m.SenderId != me
                    && !db.Blocks.Any(b => b.BlockerId == me && b.BlockedId == m.SenderId)
                    && m.CreatedAt > (db.ConversationReads.Where(r => r.ConversationId == c.Id && r.UserId == me).Select(r => (DateTimeOffset?)r.LastReadAt).FirstOrDefault() ?? Epoch)),
            })
            .ToListAsync(ct).ConfigureAwait(false);

        return rows
            .Select(r => new ChatSummaryDto(
                ChatKeys.For(r.Type, r.RefId),
                r.Type,
                r.RefId,
                titles[r.RefId].Title,
                titles[r.RefId].Category,
                r.Last is null ? null : new LastMessageDto(r.Last.DisplayName, r.Last.SenderId == me, Preview(r.Last.Body), r.Last.Kind, r.Last.CreatedAt),
                r.Unread,
                r.Last?.CreatedAt ?? r.CreatedAt))
            .OrderByDescending(c => c.UpdatedAt)
            .ToList();
    }

    public async Task<int> MemberCountAsync(Conversation convo, CancellationToken ct) => convo.Type == ConversationType.Card
        ? await db.CardMembers.CountAsync(m => m.CardId == convo.RefId && m.Status == MembershipStatus.Active, ct).ConfigureAwait(false)
        : await db.PlanParticipants.CountAsync(p => p.PlanId == convo.RefId && p.Status == MembershipStatus.Active, ct).ConfigureAwait(false);

    /// <summary>Everyone who may read the chat now, minus people who blocked the sender (they never see those messages).</summary>
    private async Task<List<Guid>> RecipientsAsync(Conversation convo, Guid senderId, CancellationToken ct)
    {
        var members = convo.Type == ConversationType.Card
            ? db.CardMembers.Where(m => m.CardId == convo.RefId && m.Status == MembershipStatus.Active).Select(m => m.UserId)
            : db.PlanParticipants.Where(p => p.PlanId == convo.RefId && p.Status == MembershipStatus.Active).Select(p => p.UserId);
        return await members
            .Where(id => !db.Blocks.Any(b => b.BlockerId == id && b.BlockedId == senderId))
            .Where(id => db.Users.Any(u => u.Id == id && u.BannedAt == null))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private static string Preview(string body) => body.Length <= 120 ? body : string.Concat(body.AsSpan(0, 119), "…");
}
