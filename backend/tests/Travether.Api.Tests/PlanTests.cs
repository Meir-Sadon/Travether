using System.Net;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Cards;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class PlanTests(PostgresFixture pg) : IAsyncLifetime
{
    private static readonly object ThaPhaeGate = new { lat = 18.7877, lng = 98.9933 };

    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static object NewPlan(
        int inDays = 6, string time = "09:00", int seatLimit = 4, string precision = "exact", Guid[]? participantIds = null,
        object? origin = null, string category = "hike", string title = "Sunrise hike to Doi Suthep") => new
        {
            title,
            category,
            origin = origin ?? ThaPhaeGate,
            originName = "Tha Phae Gate",
            originAreaLabel = "Old City, Chiang Mai",
            destination = "Wat Phra That Doi Suthep",
            destinationPrecision = precision,
            date = Today.AddDays(inDays),
            time,
            purpose = "Monk's trail up, breakfast after.",
            seatLimit,
            audience = "open",
            participantIds = participantIds ?? [],
        };

    private sealed record Group(CardDto Card, HttpClient Owner, Guid OwnerId);

    private async Task<Group> NewGroupAsync(string visibility = "public", int startInDays = 5)
    {
        var (owner, auth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(visibility, startInDays))).ReadAsync<CardDto>();
        return new Group(card, owner, auth.User!.Id);
    }

    private async Task<(HttpClient Client, Guid Id)> AddMemberAsync(Group g, string name = "Member", CardRole role = CardRole.Member)
    {
        var (client, auth) = await factory.SignUpAsync(name);
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = g.Card.Id, UserId = auth.User!.Id, Role = role, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return (client, auth.User!.Id);
    }

    private static async Task<PlanDto> CreateAsync(HttpClient client, Guid cardId, object plan)
    {
        var res = await client.PostJsonAsync($"/api/cards/{cardId}/plans", plan);
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return await res.ReadAsync<PlanDto>();
    }

    [Fact]
    public async Task A_member_hosts_a_plan_in_local_time_and_brings_card_members()
    {
        var g = await NewGroupAsync();
        var (_, lenaId) = await AddMemberAsync(g, "Lena");

        var plan = await CreateAsync(g.Owner, g.Card.Id, NewPlan(participantIds: [lenaId, g.OwnerId]));

        Assert.Equal("host", plan.Access);
        Assert.Equal("Asia/Bangkok", plan.TimeZoneId);
        Assert.Equal(new TimeOnly(9, 0), plan.LocalTime);
        Assert.Equal(TimeSpan.FromHours(7), plan.StartsAt.ToOffset(TimeSpan.FromHours(7)).Offset);
        Assert.Equal(2, plan.StartsAt.UtcDateTime.Hour);
        Assert.Equal(2, plan.SeatsTaken);
        Assert.Equal([g.OwnerId, lenaId], plan.Participants!.Select(p => p.Id));
        Assert.Equal(18.7877, plan.MeetingPoint!.Lat, 4);
        Assert.Equal("Tha Phae Gate", plan.MeetingPoint.Name);
        Assert.Equal("Wat Phra That Doi Suthep", plan.Destination);
        Assert.True(plan.CanManage);

        await using var db = pg.CreateDbContext();
        Assert.True(await db.Conversations.AnyAsync(c => c.Type == ConversationType.Plan && c.RefId == plan.Id));
        var tile = Assert.Single(await (await g.Owner.GetAsync("/api/cards")).ReadAsync<List<MyCardDto>>());
        Assert.Equal(1, tile.PlanCount);
    }

    [Fact]
    public async Task Outsiders_see_the_area_and_a_rounded_distance_but_not_the_meeting_point()
    {
        var g = await NewGroupAsync("inviteOnly");
        var exact = await CreateAsync(g.Owner, g.Card.Id, NewPlan());
        var regional = await CreateAsync(g.Owner, g.Card.Id, NewPlan(precision: "regional"));
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        // ~2.4 km north of the gate.
        var seen = await (await stranger.GetAsync($"/api/plans/{exact.Id}?lat=18.8097&lng=98.9933")).ReadAsync<PlanDto>();
        var anonymous = await (await factory.CreateApiClient().GetAsync($"/api/plans/{regional.Id}")).ReadAsync<PlanDto>();

        Assert.Equal("public", seen.Access);
        Assert.Null(seen.MeetingPoint);
        Assert.Null(seen.Destination);
        Assert.Null(seen.Participants);
        Assert.Null(seen.CardName); // the card is invite-only
        Assert.Equal("Old City, Chiang Mai", seen.AreaLabel);
        Assert.InRange(seen.Distance!.Km, 2, 3);
        Assert.False(seen.CanSelfJoin);
        Assert.Equal("Wat Phra That Doi Suthep", anonymous.Destination);
        Assert.Null(anonymous.MeetingPoint);
    }

    [Theory]
    [InlineData(20, "09:00", 4, "PlanOutsideTrip")]
    [InlineData(6, "09:00", 1, "InvalidSeatLimit")]
    [InlineData(6, "09:00", 101, "InvalidSeatLimit")]
    public async Task Plans_are_validated(int inDays, string time, int seats, string code)
    {
        var g = await NewGroupAsync();

        var res = await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", NewPlan(inDays, time, seats));

        Assert.Equal(code, await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Plans_cant_start_in_the_past_or_hold_more_people_than_seats()
    {
        var g = await NewGroupAsync(startInDays: 0);
        var (_, a) = await AddMemberAsync(g, "A");
        var (_, b) = await AddMemberAsync(g, "B");
        var (_, stranger) = await factory.SignUpAsync("Stranger");

        // Midnight in Bangkok on the card's first day is always behind UTC "now".
        Assert.Equal("PlanInPast", await (await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", NewPlan(0, "00:00"))).ErrorCodeAsync());
        Assert.Equal("SeatLimitBelowParticipants", await (await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", NewPlan(3, seatLimit: 2, participantIds: [a, b]))).ErrorCodeAsync());
        Assert.Equal("NotCardMembers", await (await g.Owner.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", NewPlan(3, participantIds: [stranger.User!.Id]))).ErrorCodeAsync());
    }

    [Fact]
    public async Task Only_card_members_host_and_list_plans()
    {
        var g = await NewGroupAsync();
        await CreateAsync(g.Owner, g.Card.Id, NewPlan());
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PostJsonAsync($"/api/cards/{g.Card.Id}/plans", NewPlan())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/cards/{g.Card.Id}/plans")).StatusCode);
        var list = await (await g.Owner.GetAsync($"/api/cards/{g.Card.Id}/plans")).ReadAsync<List<PlanSummaryDto>>();
        Assert.True(Assert.Single(list).Joined);
    }

    [Fact]
    public async Task Card_members_take_free_seats_without_asking()
    {
        var g = await NewGroupAsync();
        var plan = await CreateAsync(g.Owner, g.Card.Id, NewPlan(seatLimit: 2));
        var (member, _) = await AddMemberAsync(g, "Member");
        var (late, _) = await AddMemberAsync(g, "Late");
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        var before = await (await member.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>();
        Assert.True(before.CanSelfJoin);
        Assert.NotNull(before.Participants);
        Assert.Null(before.MeetingPoint);

        var joined = await (await member.PostAsync($"/api/plans/{plan.Id}/join", null)).ReadAsync<PlanDto>();
        Assert.Equal("participant", joined.Access);
        Assert.NotNull(joined.MeetingPoint);
        Assert.Equal(PlanStatus.Full, joined.Status);

        Assert.Equal("PlanNotOpen", await (await late.PostAsync($"/api/plans/{plan.Id}/join", null)).ErrorCodeAsync());
        Assert.Equal("NotCardMember", await (await stranger.PostAsync($"/api/plans/{plan.Id}/join", null)).ErrorCodeAsync());
        Assert.Equal("AlreadyParticipant", await (await member.PostAsync($"/api/plans/{plan.Id}/join", null)).ErrorCodeAsync());
        Assert.Equal("HostCannotLeave", await (await g.Owner.PostAsync($"/api/plans/{plan.Id}/leave", null)).ErrorCodeAsync());

        var left = await (await member.PostAsync($"/api/plans/{plan.Id}/leave", null)).ReadAsync<PlanDto>();
        Assert.Equal(PlanStatus.Open, left.Status);
        Assert.Null(left.MeetingPoint);

        Assert.Equal(HttpStatusCode.OK, (await late.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await late.PostAsync($"/api/plans/{plan.Id}/leave", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);
    }

    [Fact]
    public async Task The_host_and_card_admins_edit_and_cancel()
    {
        var g = await NewGroupAsync();
        var (host, _) = await AddMemberAsync(g, "Host");
        var (member, _) = await AddMemberAsync(g, "Member");
        var plan = await CreateAsync(host, g.Card.Id, NewPlan());

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PatchJsonAsync($"/api/plans/{plan.Id}", new { title = "Mine" })).StatusCode);

        var edited = await (await host.PatchJsonAsync($"/api/plans/{plan.Id}", new { title = "Doi Suthep at dawn", time = "06:30", seatLimit = 6, destinationPrecision = "regional" })).ReadAsync<PlanDto>();
        Assert.Equal("Doi Suthep at dawn", edited.Title);
        Assert.Equal(new TimeOnly(6, 30), edited.LocalTime);
        Assert.Equal(6, edited.SeatLimit);
        Assert.Equal(LocationPrecision.Regional, edited.DestinationPrecision);

        // The card owner isn't the host but acts for the group.
        var cancelled = await (await g.Owner.PostAsync($"/api/plans/{plan.Id}/cancel", null)).ReadAsync<PlanDto>();
        Assert.Equal(PlanStatus.Cancelled, cancelled.Status);
        Assert.Equal("PlanClosed", await (await host.PostAsync($"/api/plans/{plan.Id}/cancel", null)).ErrorCodeAsync());
        Assert.Equal("PlanClosed", await (await host.PatchJsonAsync($"/api/plans/{plan.Id}", new { title = "Back on" })).ErrorCodeAsync());
        Assert.Empty(await (await host.GetAsync($"/api/cards/{g.Card.Id}/plans")).ReadAsync<List<PlanSummaryDto>>());
    }

    [Fact]
    public async Task Removed_card_members_lose_their_seat_in_the_cards_plans()
    {
        var g = await NewGroupAsync();
        var (member, memberId) = await AddMemberAsync(g);
        var plan = await CreateAsync(g.Owner, g.Card.Id, NewPlan(participantIds: [memberId]));

        Assert.Equal(HttpStatusCode.OK, (await g.Owner.DeleteAsync($"/api/cards/{g.Card.Id}/members/{memberId}")).StatusCode);

        var seen = await (await member.GetAsync($"/api/plans/{plan.Id}")).ReadAsync<PlanDto>();
        Assert.Equal("public", seen.Access);
        Assert.Null(seen.MeetingPoint);
    }
}
