using System.Net;
using System.Text;
using Travether.Api.Cards;
using Travether.Api.Domain;
using Travether.Api.Plans;
using Travether.Api.Profiles;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class PlanCalendarTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task The_calendar_file_shows_the_meeting_point_only_to_participants()
    {
        var (host, _) = await factory.SignUpAsync("Host");
        var card = await (await host.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var plan = await (await host.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();

        var res = await host.GetAsync($"/api/plans/{plan.Id}/calendar.ics");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/calendar", res.Content.Headers.ContentType!.MediaType);
        Assert.Equal("sunrise-hike-to-doi-suthep.ics", res.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var mine = await res.Content.ReadAsStringAsync();
        Assert.Contains($"UID:plan-{plan.Id:N}@travether", mine, StringComparison.Ordinal);
        Assert.Contains($"DTSTART:{plan.StartsAt.UtcDateTime:yyyyMMdd'T'HHmmss}Z", mine, StringComparison.Ordinal);
        Assert.Contains("LOCATION:Tha Phae Gate\\, Old City\\, Chiang Mai", mine, StringComparison.Ordinal);
        Assert.Contains("GEO:18.7877;98.9933", mine, StringComparison.Ordinal);
        Assert.Contains("STATUS:CONFIRMED", mine, StringComparison.Ordinal);

        var visitor = await (await factory.CreateApiClient().GetAsync($"/api/plans/{plan.Id}/calendar.ics")).Content.ReadAsStringAsync();
        Assert.Contains("LOCATION:Old City\\, Chiang Mai", visitor, StringComparison.Ordinal);
        Assert.DoesNotContain("Tha Phae", visitor, StringComparison.Ordinal);
        Assert.DoesNotContain("GEO:", visitor, StringComparison.Ordinal);

        await host.PostAsync($"/api/plans/{plan.Id}/cancel", null);
        var cancelled = await (await host.GetAsync($"/api/plans/{plan.Id}/calendar.ics")).Content.ReadAsStringAsync();
        Assert.Contains("STATUS:CANCELLED", cancelled, StringComparison.Ordinal);
        Assert.DoesNotContain("VALARM", cancelled, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await host.GetAsync($"/api/plans/{Guid.NewGuid()}/calendar.ics")).StatusCode);
    }

    [Fact]
    public void Long_lines_fold_at_75_octets_and_text_is_escaped()
    {
        var host = new PersonDto(Guid.NewGuid(), "Noa", 30, "IL", null, []);
        var plan = new PlanDto(
            Guid.NewGuid(), Guid.NewGuid(), null, "Ride; to the 🏝️ beach, all day", PlanCategory.DayTrip,
            new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero), "Asia/Bangkok", new DateOnly(2026, 10, 20), new TimeOnly(9, 0),
            "Ao Nang, Krabi", null, null, "Railay", LocationPrecision.Exact, new string('ש', 120), 4, 1, PlanAudience.Open,
            PlanStatus.Open, "visitor", false, false, host, null, null, null);

        var ics = PlanCalendar.Build(plan, "https://travether.app/plans/x", DateTimeOffset.UtcNow);

        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains(@"SUMMARY:Ride\; to the 🏝️ beach\, all day", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20261020T100000Z", ics, StringComparison.Ordinal);
        Assert.All(ics.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, line));
        var unfolded = ics.Replace("\r\n ", "", StringComparison.Ordinal);
        Assert.Contains($"DESCRIPTION:To: Railay\\n{new string('ש', 120)}\\nHosted by Noa", unfolded, StringComparison.Ordinal);
    }
}
