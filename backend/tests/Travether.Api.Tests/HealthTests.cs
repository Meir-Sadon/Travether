using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Travether.Api.Tests;

public sealed class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_returns_ok()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", body!["status"]);
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_spa()
    {
        var client = factory.CreateClient();

        var res = await client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
