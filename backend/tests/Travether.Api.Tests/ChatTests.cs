using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Travether.Api.Auth;
using Travether.Api.Cards;
using Travether.Api.Chat;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class ChatTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Group(HttpClient Owner, Guid OwnerId, CardDto Card, HttpClient Member, Guid MemberId);

    private async Task<Group> GroupAsync()
    {
        var (owner, ownerAuth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard())).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = card.Id, UserId = memberAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new Group(owner, ownerAuth.User!.Id, card, member, memberAuth.User!.Id);
    }

    private static string CardChat(Guid id) => $"card/{id}";

    [Fact]
    public async Task Trip_members_chat_and_see_unread_counts()
    {
        var g = await GroupAsync();
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        var sent = await (await g.Owner.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "  Khao soi at 7?  " })).ReadAsync<MessageDto>();
        Assert.Equal("Khao soi at 7?", sent.Body);
        Assert.Equal("MessageEmpty", await (await g.Owner.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "   " })).ErrorCodeAsync());

        var chat = await (await g.Member.GetAsync($"/api/chats/{CardChat(g.Card.Id)}")).ReadAsync<ChatDto>();
        Assert.Equal("Chiang Mai Crew", chat.Title);
        Assert.Equal(2, chat.MemberCount);
        Assert.Equal("Khao soi at 7?", Assert.Single(chat.Messages).Body);

        var list = Assert.Single(await (await g.Member.GetAsync("/api/chats")).ReadAsync<List<ChatSummaryDto>>());
        Assert.Equal($"card-{g.Card.Id}", list.Key);
        Assert.Equal(1, list.Unread);
        Assert.False(list.Last!.Mine);
        Assert.Equal(0, Assert.Single(await (await g.Owner.GetAsync("/api/chats")).ReadAsync<List<ChatSummaryDto>>()).Unread);

        Assert.Equal(HttpStatusCode.NoContent, (await g.Member.PostAsync($"/api/chats/{CardChat(g.Card.Id)}/read", null)).StatusCode);
        Assert.Equal(0, Assert.Single(await (await g.Member.GetAsync("/api/chats")).ReadAsync<List<ChatSummaryDto>>()).Unread);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/chats/{CardChat(g.Card.Id)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "hi" })).StatusCode);
    }

    [Fact]
    public async Task Plan_chat_opens_for_participants_with_the_meeting_point()
    {
        var g = await GroupAsync();
        var plan = await (await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();

        Assert.Equal(HttpStatusCode.NotFound, (await g.Member.GetAsync($"/api/chats/plan/{plan.Id}")).StatusCode);
        await g.Member.PostAsync($"/api/plans/{plan.Id}/join", null);

        var chat = await (await g.Member.GetAsync($"/api/chats/plan/{plan.Id}")).ReadAsync<ChatDto>();
        Assert.Equal("Tha Phae Gate", chat.MeetingPoint!.Name);
        Assert.Equal(2, chat.MemberCount);
    }

    [Fact]
    public async Task Removed_members_lose_the_chat_and_blocked_senders_are_hidden()
    {
        var g = await GroupAsync();
        await g.Member.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "From member" });
        await g.Owner.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "From owner" });
        await using (var db = pg.CreateDbContext())
        {
            db.Blocks.Add(new Block { BlockerId = g.MemberId, BlockedId = g.OwnerId });
            await db.SaveChangesAsync();
        }

        var seen = await (await g.Member.GetAsync($"/api/chats/{CardChat(g.Card.Id)}")).ReadAsync<ChatDto>();
        Assert.Equal(["From member"], seen.Messages.Select(m => m.Body));

        await g.Owner.DeleteAsync($"/api/cards/{g.Card.Id}/members/{g.MemberId}");
        Assert.Equal(HttpStatusCode.NotFound, (await g.Member.GetAsync($"/api/chats/{CardChat(g.Card.Id)}")).StatusCode);
        Assert.Empty(await (await g.Member.GetAsync("/api/chats")).ReadAsync<List<ChatSummaryDto>>());
    }

    [Fact]
    public async Task People_share_their_own_number_only_by_choice()
    {
        var g = await GroupAsync();

        Assert.Equal("PhoneRequired", await (await g.Member.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/contact", new { kind = "whatsapp" })).ErrorCodeAsync());
        await g.Member.PatchJsonAsync("/api/me", new { phone = "+972501234567" });

        var shared = await (await g.Member.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/contact", new { kind = "whatsapp" })).ReadAsync<MessageDto>();
        Assert.Equal(MessageKind.ContactWhatsapp, shared.Kind);
        Assert.Equal("+972501234567", shared.Body);
    }

    [Fact]
    public async Task New_messages_are_pushed_to_members_only()
    {
        var g = await GroupAsync();
        var (_, strangerAuth) = await factory.SignUpAsync("Stranger");
        var (memberInbox, memberConn) = await ConnectAsync(g.MemberId);
        var (strangerInbox, strangerConn) = await ConnectAsync(strangerAuth.User!.Id);
        await using var _1 = memberConn;
        await using var _2 = strangerConn;

        await g.Owner.PostJsonAsync($"/api/chats/{CardChat(g.Card.Id)}/messages", new { body = "Live!" });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pushed = await memberInbox.Reader.ReadAsync(timeout.Token);
        Assert.Equal($"card-{g.Card.Id}", pushed.Chat);
        Assert.Equal("Live!", pushed.Message.Body);
        Assert.False(strangerInbox.Reader.TryRead(out _));
    }

    [Fact]
    public async Task The_inbox_lists_requests_to_decide_and_requests_sent()
    {
        var g = await GroupAsync();
        var plan = await (await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();
        var (asker, _) = await factory.SignUpAsync("Asker");
        await asker.PostJsonAsync($"/api/cards/{g.Card.Id}/requests", new { message = "Room for one?" });
        await asker.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { message = "Hike!" });

        var owner = await (await g.Owner.GetAsync("/api/inbox/requests")).ReadAsync<InboxRequestsDto>();
        var mine = await (await asker.GetAsync("/api/inbox/requests")).ReadAsync<InboxRequestsDto>();
        var member = await (await g.Member.GetAsync("/api/inbox/requests")).ReadAsync<InboxRequestsDto>();

        Assert.Equal([RequestTarget.Card, RequestTarget.Plan], owner.Incoming.Select(r => r.Target));
        Assert.Equal(["Room for one?", "Hike!"], owner.Incoming.Select(r => r.Message));
        Assert.Equal(2, mine.Outgoing.Count);
        Assert.Empty(mine.Incoming);
        Assert.Empty(member.Incoming);
    }

    private async Task<(Channel<MessageEvent> Inbox, HubConnection Connection)> ConnectAsync(Guid userId)
    {
        string token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Travether.Api.Data.TravetherDbContext>();
            var user = await db.Users.FindAsync(userId);
            token = scope.ServiceProvider.GetRequiredService<TokenService>().CreateSession(user!, out _);
        }

        var inbox = Channel.CreateUnbounded<MessageEvent>();
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/chat"), o =>
            {
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.Headers["Cookie"] = $"{SessionCookie.Name}={token}";
            })
            .Build();
        connection.On<MessageEvent>("message", e => inbox.Writer.TryWrite(e));
        await connection.StartAsync();
        return (inbox, connection);
    }
}
