using System.Net;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Cards;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class CardMembershipTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private async Task<(HttpClient Owner, CardDto Card)> NewCardAsync(string visibility = "public")
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(visibility))).ReadAsync<CardDto>();
        return (owner, card);
    }

    private static async Task<Guid> RequestAsync(HttpClient client, Guid cardId, string? slug = null)
    {
        var res = await client.PostJsonAsync($"/api/cards/{cardId}/requests", new { message = "Hi! Solo hiker here.", shareSlug = slug });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.ReadAsync<CardDto>()).MyRequest!.Id;
    }

    private async Task<(HttpClient Client, Guid Id)> JoinAsync(HttpClient owner, Guid cardId, string name = "Joiner")
    {
        var (client, auth) = await factory.SignUpAsync(name);
        var requestId = await RequestAsync(client, cardId);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/card-requests/{requestId}/approve", null)).StatusCode);
        return (client, auth.User!.Id);
    }

    [Fact]
    public async Task A_request_waits_for_the_owner_and_approval_lets_them_in()
    {
        var (owner, card) = await NewCardAsync();
        var (joiner, joinerAuth) = await factory.SignUpAsync("Joiner");

        var requestId = await RequestAsync(joiner, card.Id);
        var waiting = await (await joiner.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("preview", waiting.Access);
        Assert.Equal(RequestStatus.Requested, waiting.MyRequest!.Status);
        Assert.Equal("AlreadyRequested", await (await joiner.PostJsonAsync($"/api/cards/{card.Id}/requests", new { })).ErrorCodeAsync());

        var ownerView = await (await owner.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal(1, ownerView.PendingRequestCount);
        var open = await (await owner.GetAsync($"/api/cards/{card.Id}/requests")).ReadAsync<List<CardRequestDto>>();
        var row = Assert.Single(open);
        Assert.Equal(joinerAuth.User!.Id, row.Person.Id);
        Assert.Equal("Hi! Solo hiker here.", row.Message);
        Assert.Equal(1, Assert.Single(await (await owner.GetAsync("/api/cards")).ReadAsync<List<MyCardDto>>()).PendingRequests);

        var approved = await (await owner.PostAsync($"/api/card-requests/{requestId}/approve", null)).ReadAsync<CardDto>();
        Assert.Equal(2, approved.MemberCount);
        Assert.Equal(0, approved.PendingRequestCount);
        Assert.Equal("AlreadyDecided", await (await owner.PostAsync($"/api/card-requests/{requestId}/reject", null)).ErrorCodeAsync());

        var inside = await (await joiner.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("member", inside.Access);
        Assert.Null(inside.MyRequest);
        Assert.Equal("AlreadyMember", await (await joiner.PostJsonAsync($"/api/cards/{card.Id}/requests", new { })).ErrorCodeAsync());
    }

    [Fact]
    public async Task Rejected_and_withdrawn_requests_can_be_made_again()
    {
        var (owner, card) = await NewCardAsync();
        var (joiner, _) = await factory.SignUpAsync("Joiner");

        var first = await RequestAsync(joiner, card.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await joiner.DeleteAsync($"/api/card-requests/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await joiner.DeleteAsync($"/api/card-requests/{first}")).StatusCode);

        var second = await RequestAsync(joiner, card.Id);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/card-requests/{second}/reject", null)).StatusCode);
        var afterReject = await (await joiner.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("preview", afterReject.Access);
        Assert.Equal(RequestStatus.Rejected, afterReject.MyRequest!.Status);

        await RequestAsync(joiner, card.Id);
    }

    [Fact]
    public async Task Invite_only_cards_take_requests_only_through_the_share_link()
    {
        var (_, card) = await NewCardAsync("inviteOnly");
        var (joiner, _) = await factory.SignUpAsync("Joiner");

        Assert.Equal(HttpStatusCode.NotFound, (await joiner.PostJsonAsync($"/api/cards/{card.Id}/requests", new { })).StatusCode);
        await RequestAsync(joiner, card.Id, card.ShareSlug);
    }

    [Fact]
    public async Task Finished_trips_take_no_requests()
    {
        var (_, card) = await NewCardAsync();
        await using (var db = pg.CreateDbContext())
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            await db.VacationCards.Where(c => c.Id == card.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.StartsOn, today.AddDays(-10)).SetProperty(c => c.EndsOn, today.AddDays(-1)));
        }

        var (joiner, _) = await factory.SignUpAsync("Late");

        Assert.Equal("TripEnded", await (await joiner.PostJsonAsync($"/api/cards/{card.Id}/requests", new { })).ErrorCodeAsync());
    }

    [Fact]
    public async Task Co_admins_decide_requests_but_members_dont()
    {
        var (owner, card) = await NewCardAsync();
        var (member, memberId) = await JoinAsync(owner, card.Id, "Member");
        var (coAdmin, coAdminId) = await JoinAsync(owner, card.Id, "CoAdmin");
        Assert.Equal(HttpStatusCode.OK, (await owner.PutJsonAsync($"/api/cards/{card.Id}/members/{coAdminId}/role", new { role = "coAdmin" })).StatusCode);

        var (joiner, _) = await factory.SignUpAsync("Joiner");
        var requestId = await RequestAsync(joiner, card.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/api/cards/{card.Id}/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"/api/card-requests/{requestId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coAdmin.PutJsonAsync($"/api/cards/{card.Id}/members/{memberId}/role", new { role = "coAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coAdmin.DeleteAsync($"/api/cards/{card.Id}/members/{memberId}")).StatusCode);

        var approved = await (await coAdmin.PostAsync($"/api/card-requests/{requestId}/approve", null)).ReadAsync<CardDto>();
        Assert.Equal(4, approved.MemberCount);
    }

    [Fact]
    public async Task Approving_someone_who_blocked_the_owner_expires_the_request()
    {
        var (owner, card) = await NewCardAsync();
        var (joiner, joinerAuth) = await factory.SignUpAsync("Joiner");
        var requestId = await RequestAsync(joiner, card.Id);
        await using (var db = pg.CreateDbContext())
        {
            var ownerId = await db.VacationCards.Where(c => c.Id == card.Id).Select(c => c.OwnerId).SingleAsync();
            db.Blocks.Add(new Block { BlockerId = joinerAuth.User!.Id, BlockedId = ownerId });
            await db.SaveChangesAsync();
        }

        Assert.Equal("RequestExpired", await (await owner.PostAsync($"/api/card-requests/{requestId}/approve", null)).ErrorCodeAsync());

        await using var check = pg.CreateDbContext();
        Assert.Equal(RequestStatus.Expired, (await check.CardRequests.SingleAsync(r => r.Id == requestId)).Status);
        Assert.False(await check.CardMembers.AnyAsync(m => m.CardId == card.Id && m.UserId == joinerAuth.User!.Id));
    }

    [Fact]
    public async Task Handing_over_ownership_makes_the_old_owner_a_co_admin()
    {
        var (owner, card) = await NewCardAsync();
        var (member, memberId) = await JoinAsync(owner, card.Id);

        Assert.Equal("CannotChangeOwnRole", await (await owner.PutJsonAsync($"/api/cards/{card.Id}/members/{card.Members![0].Person.Id}/role", new { role = "member" })).ErrorCodeAsync());

        var handedOver = await (await owner.PutJsonAsync($"/api/cards/{card.Id}/members/{memberId}/role", new { role = "owner" })).ReadAsync<CardDto>();
        Assert.Equal("coAdmin", handedOver.Access);
        Assert.Equal(memberId, handedOver.Members!.Single(m => m.Role == CardRole.Owner).Person.Id);

        var asNewOwner = await (await member.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("owner", asNewOwner.Access);
        Assert.Equal(HttpStatusCode.OK, (await member.PatchJsonAsync($"/api/cards/{card.Id}", new { name = "New hands" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PatchJsonAsync($"/api/cards/{card.Id}", new { name = "Mine again" })).StatusCode);

        await using var db = pg.CreateDbContext();
        Assert.Equal(memberId, await db.VacationCards.Where(c => c.Id == card.Id).Select(c => c.OwnerId).SingleAsync());
    }

    [Fact]
    public async Task Removed_members_lose_the_card_and_can_ask_to_come_back()
    {
        var (owner, card) = await NewCardAsync("inviteOnly");
        var (member, memberId) = await JoinInviteOnlyAsync(owner, card);

        var after = await (await owner.DeleteAsync($"/api/cards/{card.Id}/members/{memberId}")).ReadAsync<CardDto>();
        Assert.Equal(1, after.MemberCount);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/cards/{card.Id}")).StatusCode);
        Assert.Empty(await (await member.GetAsync("/api/cards")).ReadAsync<List<MyCardDto>>());

        var again = await RequestAsync(member, card.Id, card.ShareSlug);
        var back = await (await owner.PostAsync($"/api/card-requests/{again}/approve", null)).ReadAsync<CardDto>();
        Assert.Equal(2, back.MemberCount);
    }

    private async Task<(HttpClient Client, Guid Id)> JoinInviteOnlyAsync(HttpClient owner, CardDto card)
    {
        var (client, auth) = await factory.SignUpAsync("Invitee");
        var requestId = await RequestAsync(client, card.Id, card.ShareSlug);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/card-requests/{requestId}/approve", null)).StatusCode);
        return (client, auth.User!.Id);
    }

    [Fact]
    public async Task Members_can_leave_but_the_owner_cant()
    {
        var (owner, card) = await NewCardAsync();
        var (member, _) = await JoinAsync(owner, card.Id);

        Assert.Equal("OwnerCannotLeave", await (await owner.PostAsync($"/api/cards/{card.Id}/leave", null)).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await member.PostAsync($"/api/cards/{card.Id}/leave", null)).StatusCode);

        var view = await (await member.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("preview", view.Access);
        Assert.Equal(1, view.MemberCount);
    }
}
