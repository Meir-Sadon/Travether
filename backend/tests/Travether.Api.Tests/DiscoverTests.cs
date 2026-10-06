using System.Net;
using Travether.Api.Cards;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class DiscoverTests(PostgresFixture pg) : IAsyncLifetime
{
    // Each test searches around its own city so plans from other tests in the shared database don't show up.
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>A point offset from a per-test base, so tests don't see each other's plans.</summary>
    private static object At(double baseLat, double baseLng, double northKm = 0, double eastKm = 0) =>
        new { lat = baseLat + (northKm / 111.0), lng = baseLng + (eastKm / 105.0) };

    private async Task<(HttpClient Client, Guid UserId, CardDto Card)> HostAsync(string name)
    {
        var (client, auth) = await factory.SignUpAsync(name);
        var card = await (await client.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        return (client, auth.User!.Id, card);
    }

    private static async Task<PlanDto> PostAsync(HttpClient client, Guid cardId, object plan)
    {
        var res = await client.PostJsonAsync($"/api/cards/{cardId}/plans", plan);
        Assert.True(res.StatusCode == HttpStatusCode.Created, await res.Content.ReadAsStringAsync());
        return await res.ReadAsync<PlanDto>();
    }

    private static string Query(double lat, double lng, string extra = "") =>
        FormattableString.Invariant($"/api/discover?lat={lat}&lng={lng}&from={Today.AddDays(4):yyyy-MM-dd}&to={Today.AddDays(12):yyyy-MM-dd}{extra}");

    [Fact]
    public async Task Finds_open_plans_nearby_on_your_dates_nearest_first()
    {
        const double lat = 13.70, lng = 100.50; // around Bangkok
        var (host, _, card) = await HostAsync("Host");
        var near = await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 7, origin: At(lat, lng, 2), title: "Near"));
        var nearer = await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 9, origin: At(lat, lng, 0.2), title: "Nearer"));
        await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 7, origin: At(lat, lng, 60), title: "Too far"));
        await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 14, origin: At(lat, lng, 1), title: "After your trip"));
        var full = await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 7, origin: At(lat, lng, 1), title: "Full", seatLimit: 2));
        var cancelled = await PostAsync(host, card.Id, PlanTests.NewPlan(inDays: 7, origin: At(lat, lng, 1), title: "Cancelled"));
        Assert.Equal(HttpStatusCode.OK, (await host.PostAsync($"/api/plans/{cancelled.Id}/cancel", null)).StatusCode);
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await using (var db = pg.CreateDbContext())
        {
            db.CardMembers.Add(new CardMember { CardId = card.Id, UserId = memberAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/plans/{full.Id}/join", null)).StatusCode);
        var (searcher, _) = await factory.SignUpAsync("Searcher");

        var found = await (await searcher.GetAsync(Query(lat, lng))).ReadAsync<List<DiscoverPlanDto>>();

        Assert.Equal([nearer.Id, near.Id], found.Select(f => f.Plan.Id));
        Assert.True(found[0].Distance.UnderOneKm);
        Assert.Equal(2, found[1].Distance.Km);

        var byDate = await (await searcher.GetAsync(Query(lat, lng, "&sort=date&radiusKm=100"))).ReadAsync<List<DiscoverPlanDto>>();
        Assert.Equal(["Near", "Too far", "Nearer"], byDate.Select(f => f.Plan.Title));

        // Visitors can browse too; the host's own trip doesn't show its plans to them in Discover.
        Assert.Equal(2, (await (await factory.CreateApiClient().GetAsync(Query(lat, lng))).ReadAsync<List<DiscoverPlanDto>>()).Count);
        Assert.Empty(await (await host.GetAsync(Query(lat, lng))).ReadAsync<List<DiscoverPlanDto>>());
    }

    [Fact]
    public async Task Filters_by_category_and_group_size()
    {
        const double lat = 7.88, lng = 98.39; // around Phuket
        var (host, _, card) = await HostAsync("Host");
        await PostAsync(host, card.Id, PlanTests.NewPlan(origin: At(lat, lng, 1), title: "Hike", seatLimit: 8));
        await PostAsync(host, card.Id, PlanTests.NewPlan(origin: At(lat, lng, 1), title: "Dinner", category: "food", seatLimit: 4));
        var searcher = factory.CreateApiClient();

        var food = await (await searcher.GetAsync(Query(lat, lng, "&category=food"))).ReadAsync<List<DiscoverPlanDto>>();
        var small = await (await searcher.GetAsync(Query(lat, lng, "&maxSeats=4"))).ReadAsync<List<DiscoverPlanDto>>();

        Assert.Equal("Dinner", Assert.Single(food).Plan.Title);
        Assert.Equal("Dinner", Assert.Single(small).Plan.Title);
    }

    [Fact]
    public async Task Blocked_hosts_dont_show_up()
    {
        const double lat = 12.57, lng = 99.95; // around Hua Hin
        var (host, hostId, card) = await HostAsync("Host");
        await PostAsync(host, card.Id, PlanTests.NewPlan(origin: At(lat, lng, 1)));
        var (searcher, auth) = await factory.SignUpAsync("Searcher");
        Assert.Single(await (await searcher.GetAsync(Query(lat, lng))).ReadAsync<List<DiscoverPlanDto>>());

        await using (var db = pg.CreateDbContext())
        {
            db.Blocks.Add(new Block { BlockerId = hostId, BlockedId = auth.User!.Id });
            await db.SaveChangesAsync();
        }

        Assert.Empty(await (await searcher.GetAsync(Query(lat, lng))).ReadAsync<List<DiscoverPlanDto>>());
    }

    [Theory]
    [InlineData("/api/discover?lat=95&lng=0&from=2026-01-01&to=2026-01-02", "InvalidLocation")]
    [InlineData("/api/discover?lat=1&lng=1&from=2026-01-05&to=2026-01-02", "DatesOutOfOrder")]
    public async Task Bad_searches_are_rejected(string url, string code)
    {
        Assert.Equal(code, await (await factory.CreateApiClient().GetAsync(url)).ErrorCodeAsync());
    }
}
