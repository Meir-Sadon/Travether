using System.Net;
using System.Net.Http.Json;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class HealthTests(PostgresFixture pg)
{
    [Fact]
    public async Task Health_reports_api_and_database_ok()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", body!["status"]);
        Assert.Equal("ok", body["database"]);
    }

    [Fact]
    public async Task Health_stays_up_when_the_database_is_down()
    {
        await using var factory = new ApiFactory("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");
        var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<Dictionary<string, string>>("/api/health");

        Assert.Equal("ok", body!["status"]);
        Assert.Equal("unavailable", body["database"]);
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_spa()
    {
        await using var factory = new ApiFactory(pg.ConnectionString);
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
