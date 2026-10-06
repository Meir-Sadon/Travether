using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Cards;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class CardTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static object NewCard(string visibility = "public", int startInDays = 5, int days = 10, string[]? regions = null) => new
    {
        name = "Chiang Mai Crew",
        countryCode = "TH",
        regions = regions ?? ["Chiang Mai", " Pai ", "chiang mai"],
        startsOn = Today.AddDays(startInDays),
        endsOn = Today.AddDays(startInDays + days),
        description = "Hikes, khao soi, a cooking class.",
        visibility,
    };

    private async Task AddMemberAsync(Guid cardId, Guid userId, CardRole role = CardRole.Member)
    {
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = cardId, UserId = userId, Role = role, Status = MembershipStatus.Active });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Creating_a_card_makes_you_its_owner_and_opens_its_chat()
    {
        var (client, me) = await factory.SignUpAsync("Owner");

        var res = await client.PostJsonAsync("/api/cards", NewCard());
        var card = await res.ReadAsync<CardDto>();

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("owner", card.Access);
        Assert.Equal(["Chiang Mai", "Pai"], card.Regions); // trimmed, de-duplicated
        Assert.Equal(CardRules.SlugLength, card.ShareSlug!.Length);
        Assert.Equal(me.User!.Id, Assert.Single(card.Members!).Person.Id);

        await using var db = pg.CreateDbContext();
        Assert.True(await db.Conversations.AnyAsync(c => c.Type == ConversationType.Card && c.RefId == card.Id));

        var mine = await (await client.GetAsync("/api/cards")).ReadAsync<List<MyCardDto>>();
        var tile = Assert.Single(mine);
        Assert.Equal(CardRole.Owner, tile.Role);
        Assert.Equal(1, tile.MemberCount);
    }

    [Theory]
    [InlineData(5, -1, "DatesOutOfOrder")]
    [InlineData(-30, 5, "TripInPast")]
    [InlineData(5, 400, "TripTooLong")]
    public async Task Card_dates_are_validated(int startInDays, int days, string code)
    {
        var (client, _) = await factory.SignUpAsync("Dates");

        var res = await client.PostJsonAsync("/api/cards", NewCard(startInDays: startInDays, days: days));

        Assert.Equal(code, await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task A_card_needs_at_least_one_region()
    {
        var (client, _) = await factory.SignUpAsync("Regions");

        var res = await client.PostJsonAsync("/api/cards", NewCard(regions: ["  "]));

        Assert.Equal("RegionsRequired", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Public_cards_show_a_preview_without_members_or_share_link()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard("public"))).ReadAsync<CardDto>();
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        var asVisitor = await (await factory.CreateApiClient().GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        var asStranger = await (await stranger.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();

        foreach (var view in new[] { asVisitor, asStranger })
        {
            Assert.Equal("preview", view.Access);
            Assert.Equal("Chiang Mai Crew", view.Name);
            Assert.Equal(1, view.MemberCount);
            Assert.Null(view.Members);
            Assert.Null(view.ShareSlug);
        }
    }

    [Fact]
    public async Task Invite_only_cards_open_only_through_the_share_link()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard("inviteOnly"))).ReadAsync<CardDto>();
        var visitor = factory.CreateApiClient();

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/cards/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/cards/share/notTheSlug")).StatusCode);
        var preview = await (await visitor.GetAsync($"/api/cards/share/{card.ShareSlug}")).ReadAsync<CardDto>();
        Assert.Equal("preview", preview.Access);
        Assert.Null(preview.ShareSlug);

        // A new link retires the old one.
        var rotated = await (await owner.PostAsync($"/api/cards/{card.Id}/share-link", null)).ReadAsync<CardDto>();
        Assert.NotEqual(card.ShareSlug, rotated.ShareSlug);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/cards/share/{card.ShareSlug}")).StatusCode);
    }

    [Fact]
    public async Task Someone_blocked_by_the_owner_cant_see_the_card()
    {
        var (owner, ownerAuth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard("public"))).ReadAsync<CardDto>();
        var (blocked, blockedAuth) = await factory.SignUpAsync("Blocked");
        await using (var db = pg.CreateDbContext())
        {
            db.Blocks.Add(new Block { BlockerId = ownerAuth.User!.Id, BlockedId = blockedAuth.User!.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await blocked.GetAsync($"/api/cards/{card.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await blocked.GetAsync($"/api/cards/share/{card.ShareSlug}")).StatusCode);
    }

    [Fact]
    public async Task Members_see_the_inside_but_only_the_owner_edits()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard("inviteOnly"))).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        var (coAdmin, coAdminAuth) = await factory.SignUpAsync("CoAdmin");
        await AddMemberAsync(card.Id, memberAuth.User!.Id);
        await AddMemberAsync(card.Id, coAdminAuth.User!.Id, CardRole.CoAdmin);

        var inside = await (await member.GetAsync($"/api/cards/{card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("member", inside.Access);
        Assert.Equal([CardRole.Owner, CardRole.CoAdmin, CardRole.Member], inside.Members!.Select(m => m.Role));
        Assert.NotNull(inside.ShareSlug);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PatchJsonAsync($"/api/cards/{card.Id}", new { name = "Mine now" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await coAdmin.DeleteAsync($"/api/cards/{card.Id}")).StatusCode);

        var edited = await (await owner.PatchJsonAsync($"/api/cards/{card.Id}", new { name = "CM Crew", visibility = "public", description = "" })).ReadAsync<CardDto>();
        Assert.Equal("CM Crew", edited.Name);
        Assert.Equal(CardVisibility.Public, edited.Visibility);
        Assert.Null(edited.Description);
    }

    [Fact]
    public async Task Deleting_a_card_hides_it_cancels_its_plans_and_expires_requests()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard("public"))).ReadAsync<CardDto>();
        var (_, requester) = await factory.SignUpAsync("Requester");
        await using (var db = pg.CreateDbContext())
        {
            db.CardRequests.Add(new CardRequest { Id = Guid.NewGuid(), CardId = card.Id, UserId = requester.User!.Id, Status = RequestStatus.Requested });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/cards/{card.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/cards/{card.Id}")).StatusCode);
        Assert.Empty(await (await owner.GetAsync("/api/cards")).ReadAsync<List<MyCardDto>>());
        await using var check = pg.CreateDbContext();
        var request = await check.CardRequests.IgnoreQueryFilters().SingleAsync(r => r.CardId == card.Id);
        Assert.Equal(RequestStatus.Expired, request.Status);
    }

    [Fact]
    public async Task The_owner_can_upload_a_cover()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", NewCard())).ReadAsync<CardDto>();
        var png = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII="));
        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var updated = await (await owner.PostAsync($"/api/cards/{card.Id}/cover", new MultipartFormDataContent { { png, "file", "cover.png" } })).ReadAsync<CardDto>();

        Assert.StartsWith("/uploads/covers/", updated.CoverUrl);
    }
}
