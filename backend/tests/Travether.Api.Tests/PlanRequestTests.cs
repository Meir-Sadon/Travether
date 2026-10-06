using System.Net;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Cards;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class PlanRequestTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Traveler(HttpClient Client, Guid Id, CardDto Card);

    /// <summary>Someone with their own trip.</summary>
    private async Task<Traveler> TravelerAsync(string name)
    {
        var (client, auth) = await factory.SignUpAsync(name);
        var card = await (await client.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        return new Traveler(client, auth.User!.Id, card);
    }

    private async Task<(HttpClient Client, Guid Id)> AddMemberAsync(Guid cardId, string name, CardRole role = CardRole.Member)
    {
        var (client, auth) = await factory.SignUpAsync(name);
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = cardId, UserId = auth.User!.Id, Role = role, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return (client, auth.User!.Id);
    }

    private static async Task<PlanDto> PlanAsync(Traveler host, int seats = 4, string audience = "open")
    {
        var plan = PlanTests.NewPlan(seatLimit: seats);
        var body = plan.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(plan));
        body["audience"] = audience;
        var res = await host.Client.PostJsonAsync($"/api/cards/{host.Card.Id}/plans", body);
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return await res.ReadAsync<PlanDto>();
    }

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, Guid planId, Guid? sourceCardId = null, params Guid[] party) =>
        client.PostJsonAsync($"/api/plans/{planId}/requests", new { message = "Can we come?", sourceCardId, partyUserIds = party });

    [Fact]
    public async Task A_solo_traveler_asks_and_approval_reveals_the_meeting_point()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        var (solo, soloAuth) = await factory.SignUpAsync("Solo");

        var asked = await (await RequestAsync(solo, plan.Id)).ReadAsync<PlanDto>();
        Assert.Equal(RequestStatus.Requested, asked.MyRequest!.Status);
        Assert.Null(asked.MeetingPoint);
        Assert.Equal("AlreadyRequested", await (await RequestAsync(solo, plan.Id)).ErrorCodeAsync());

        var hostView = await (await host.Client.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>();
        Assert.Equal(1, hostView.PendingRequestCount);
        var open = Assert.Single(await (await host.Client.GetAsync($"/api/plans/{plan.Id}/requests")).ReadAsync<List<PlanRequestDto>>());
        Assert.Equal(soloAuth.User!.Id, open.Requester.Id);
        Assert.Equal("Can we come?", open.Message);
        Assert.Empty(open.Party);

        var approved = await (await host.Client.PostAsync($"/api/plan-requests/{open.Id}/approve", null)).ReadAsync<PlanDto>();
        Assert.Equal(2, approved.SeatsTaken);
        Assert.Equal(0, approved.PendingRequestCount);
        Assert.Equal("AlreadyDecided", await (await host.Client.PostAsync($"/api/plan-requests/{open.Id}/reject", null)).ErrorCodeAsync());

        var inside = await (await solo.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>();
        Assert.Equal("participant", inside.Access);
        Assert.Equal("Tha Phae Gate", inside.MeetingPoint!.Name);
        Assert.Null(inside.MyRequest);
        Assert.Equal("AlreadyParticipant", await (await RequestAsync(solo, plan.Id)).ErrorCodeAsync());
    }

    [Fact]
    public async Task Groups_only_plans_take_requests_from_cards_with_their_party()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host, seats: 4, audience: "groupsOnly");
        var guest = await TravelerAsync("Guest");
        var (friend, friendId) = await AddMemberAsync(guest.Card.Id, "Friend");
        var (_, stranger) = await factory.SignUpAsync("Stranger");

        Assert.Equal("GroupsOnly", await (await RequestAsync(guest.Client, plan.Id)).ErrorCodeAsync());
        Assert.Equal("SourceCardNotYours", await (await RequestAsync(friend, plan.Id, host.Card.Id)).ErrorCodeAsync());
        Assert.Equal("PartyNotInSourceCard", await (await RequestAsync(guest.Client, plan.Id, guest.Card.Id, stranger.User!.Id)).ErrorCodeAsync());
        Assert.Equal("NotEnoughSeats", await (await RequestAsync(guest.Client, plan.Id, guest.Card.Id, friendId, (await AddMemberAsync(guest.Card.Id, "A")).Id, (await AddMemberAsync(guest.Card.Id, "B")).Id)).ErrorCodeAsync());

        var asked = await (await RequestAsync(guest.Client, plan.Id, guest.Card.Id, friendId)).ReadAsync<PlanDto>();
        Assert.Equal(2, asked.MyRequest!.PartySize);
        var open = Assert.Single(await (await host.Client.GetAsync($"/api/plans/{plan.Id}/requests")).ReadAsync<List<PlanRequestDto>>());
        Assert.Equal(friendId, Assert.Single(open.Party).Id);
        Assert.Equal("Chiang Mai Crew", open.SourceCardName);

        var approved = await (await host.Client.PostAsync($"/api/plan-requests/{open.Id}/approve", null)).ReadAsync<PlanDto>();
        Assert.Equal(3, approved.SeatsTaken);
        Assert.NotNull((await (await friend.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>()).MeetingPoint);
    }

    [Fact]
    public async Task Card_members_join_directly_instead_of_asking()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        var (member, _) = await AddMemberAsync(host.Card.Id, "Member");

        Assert.Equal("JoinDirectly", await (await RequestAsync(member, plan.Id)).ErrorCodeAsync());
    }

    [Fact]
    public async Task Card_admins_decide_and_plain_members_dont()
    {
        var host = await TravelerAsync("Host");
        var (hosting, _) = await AddMemberAsync(host.Card.Id, "Hosting");
        var hostingTraveler = host with { Client = hosting };
        var plan = await PlanAsync(hostingTraveler);
        var (member, _) = await AddMemberAsync(host.Card.Id, "Member");
        var (first, _) = await factory.SignUpAsync("First");
        var (second, _) = await factory.SignUpAsync("Second");
        var firstId = (await (await RequestAsync(first, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;
        var secondId = (await (await RequestAsync(second, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/plans/{plan.Id}/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"/api/plan-requests/{firstId}/approve", null)).StatusCode);

        // The card owner isn't the host but decides for the group.
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync($"/api/plan-requests/{firstId}/reject", null)).StatusCode);
        var rejected = await (await first.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>();
        Assert.Equal(RequestStatus.Rejected, rejected.MyRequest!.Status);

        Assert.Equal(HttpStatusCode.NoContent, (await second.DeleteAsync($"/api/plan-requests/{secondId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RequestAsync(second, plan.Id)).StatusCode);
    }

    [Fact]
    public async Task Filling_the_last_seat_expires_the_other_requests()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host, seats: 2);
        var (first, _) = await factory.SignUpAsync("First");
        var (second, _) = await factory.SignUpAsync("Second");
        var firstId = (await (await RequestAsync(first, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;
        await RequestAsync(second, plan.Id);

        var full = await (await host.Client.PostAsync($"/api/plan-requests/{firstId}/approve", null)).ReadAsync<PlanDto>();

        Assert.Equal(PlanStatus.Full, full.Status);
        Assert.Equal(RequestStatus.Expired, (await (await second.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>()).MyRequest!.Status);
        Assert.Equal("PlanFull", await (await RequestAsync(second, plan.Id)).ErrorCodeAsync());
    }

    [Fact]
    public async Task A_block_since_the_request_makes_it_expire()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        var (asker, askerAuth) = await factory.SignUpAsync("Asker");
        var requestId = (await (await RequestAsync(asker, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;
        await using (var db = pg.CreateDbContext())
        {
            db.Blocks.Add(new Block { BlockerId = askerAuth.User!.Id, BlockedId = host.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal("RequestExpired", await (await host.Client.PostAsync($"/api/plan-requests/{requestId}/approve", null)).ErrorCodeAsync());
    }

    [Fact]
    public async Task Requests_expire_once_the_plan_starts()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        var (asker, _) = await factory.SignUpAsync("Asker");
        var requestId = (await (await RequestAsync(asker, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;

        await using var db = pg.CreateDbContext();
        await PlanRequestSweeper.ExpireStartedAsync(db, plan.StartsAt.AddMinutes(1), default);

        Assert.Equal(RequestStatus.Expired, (await db.PlanRequests.SingleAsync(r => r.Id == requestId)).Status);
    }

    [Fact]
    public async Task The_host_removes_a_participant_but_not_themselves()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        var (asker, askerAuth) = await factory.SignUpAsync("Asker");
        var requestId = (await (await RequestAsync(asker, plan.Id)).ReadAsync<PlanDto>()).MyRequest!.Id;
        await host.Client.PostAsync($"/api/plan-requests/{requestId}/approve", null);

        Assert.Equal("HostCannotLeave", await (await host.Client.DeleteAsync($"/api/plans/{plan.Id}/participants/{host.Id}")).ErrorCodeAsync());
        var after = await (await host.Client.DeleteAsync($"/api/plans/{plan.Id}/participants/{askerAuth.User!.Id}")).ReadAsync<PlanDto>();

        Assert.Equal(1, after.SeatsTaken);
        Assert.Null((await (await asker.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>()).MeetingPoint);
    }
}
