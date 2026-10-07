using Microsoft.AspNetCore.Hosting;
using Travether.Api.Auth;
using Travether.Api.Telemetry;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class TelemetryTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task Client_config_hands_out_only_public_keys_when_set()
    {
        var off = await (await factory.CreateApiClient().GetAsync("/api/client-config")).ReadAsync<ClientConfigDto>();
        Assert.Null(off.PostHogKey);
        Assert.Null(off.SentryDsn);

        using var configured = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Telemetry:PostHogKey", "phc_test");
            b.UseSetting("Telemetry:SentryDsn", "https://key@o1.ingest.de.sentry.io/1");
            b.UseSetting("Telemetry:Environment", "staging");
        });
        var on = await (await configured.CreateClient().GetAsync("/api/client-config")).ReadAsync<ClientConfigDto>();
        Assert.Equal("phc_test", on.PostHogKey);
        Assert.Equal("https://eu.i.posthog.com", on.PostHogHost);
        Assert.Equal("https://key@o1.ingest.de.sentry.io/1", on.SentryDsn);
        Assert.Equal("staging", on.Environment);
    }

    [Fact]
    public async Task Analytics_is_opt_in_at_sign_up()
    {
        var (without, _) = await factory.SignUpAsync("Quiet");
        Assert.False((await (await without.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).Analytics);

        var with = factory.CreateApiClient();
        await with.PostJsonAsync("/api/auth/register", new
        {
            email = ApiHelpers.NewEmail("Counted"),
            password = "correct horse battery",
            displayName = "Counted",
            fullName = "Counted Example",
            dateOfBirth = "1995-05-05",
            countryCode = "IL",
            acceptTerms = true,
            allowAnalytics = true,
        });
        Assert.True((await (await with.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).Analytics);
    }
}
