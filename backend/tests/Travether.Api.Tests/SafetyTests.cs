using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Cards;
using Travether.Api.Chat;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Plans;
using Travether.Api.Safety;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class SafetyTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Crew(HttpClient Owner, Guid OwnerId, HttpClient Member, Guid MemberId, CardDto Card);

    /// <summary>A card with its owner and one member, so there is a chat to talk (and misbehave) in.</summary>
    private async Task<Crew> CrewAsync()
    {
        var (owner, ownerAuth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = card.Id, UserId = memberAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new Crew(owner, ownerAuth.User!.Id, member, memberAuth.User.Id, card);
    }

    private async Task<HttpClient> ModeratorAsync()
    {
        var (client, auth) = await factory.SignUpAsync("Mod");
        await using var db = pg.CreateDbContext();
        await db.Users.Where(u => u.Id == auth.User!.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Role, UserRole.Moderator));
        return client;
    }

    private static Task<HttpResponseMessage> ReportAsync(HttpClient client, string targetType, Guid targetId, string reason = "harassment") =>
        client.PostJsonAsync("/api/reports", new { targetType, targetId, reason, details = "Rude in the group chat" });

    private static object Registration(string email) => new
    {
        email,
        password = "correct horse battery",
        displayName = "Again",
        fullName = "Again Example",
        dateOfBirth = "1995-05-05",
        countryCode = "IL",
        acceptTerms = true,
    };

    [Fact]
    public async Task Blocking_hides_both_people_from_each_other_until_unblocked()
    {
        var (alice, aliceAuth) = await factory.SignUpAsync("Alice");
        var (bob, bobAuth) = await factory.SignUpAsync("Bob");

        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/users/{bobAuth.User!.Id}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsync($"/api/users/{bobAuth.User.Id}/block", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync($"/api/users/{bobAuth.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/users/{aliceAuth.User!.Id}")).StatusCode);
        Assert.Equal("Bob", Assert.Single(await (await alice.GetAsync("/api/me/blocks")).ReadAsync<List<BlockedUserDto>>()).DisplayName);
        Assert.Equal("CannotBlockSelf", await (await alice.PostAsync($"/api/users/{aliceAuth.User.Id}/block", null)).ErrorCodeAsync());

        await alice.DeleteAsync($"/api/users/{bobAuth.User.Id}/block");
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/api/users/{aliceAuth.User.Id}")).StatusCode);
    }

    [Fact]
    public async Task Blocking_a_host_lapses_my_open_request()
    {
        var (host, hostAuth) = await factory.SignUpAsync("Host");
        var card = await (await host.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var plan = await (await host.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();
        var (solo, _) = await factory.SignUpAsync("Solo");
        await solo.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { });

        await solo.PostAsync($"/api/users/{hostAuth.User!.Id}/block", null);

        await using var db = pg.CreateDbContext();
        Assert.Equal(RequestStatus.Expired, (await db.PlanRequests.SingleAsync(r => r.PlanId == plan.Id)).Status);
    }

    [Fact]
    public async Task You_can_report_only_what_you_can_see_and_once()
    {
        var crew = await CrewAsync();
        var sent = await (await crew.Owner.PostJsonAsync($"/api/chats/card/{crew.Card.Id}/messages", new { body = "You lot are useless" })).ReadAsync<MessageDto>();
        var (outsider, _) = await factory.SignUpAsync("Outsider");

        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(outsider, "message", sent.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReportAsync(crew.Owner, "message", sent.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(crew.Member, "message", sent.Id)).StatusCode);
        Assert.Equal("AlreadyReported", await (await ReportAsync(crew.Member, "message", sent.Id)).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await ReportAsync(outsider, "user", crew.OwnerId, "spam")).StatusCode);
    }

    [Fact]
    public async Task A_moderator_removes_a_message_and_the_author_is_told_why()
    {
        var crew = await CrewAsync();
        var sent = await (await crew.Owner.PostJsonAsync($"/api/chats/card/{crew.Card.Id}/messages", new { body = "Send me your passport photo" })).ReadAsync<MessageDto>();
        await ReportAsync(crew.Member, "message", sent.Id, "scam");
        var mod = await ModeratorAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await crew.Member.GetAsync("/api/admin/reports")).StatusCode);
        var queue = await (await mod.GetAsync("/api/admin/reports")).ReadAsync<List<ReportAdminDto>>();
        var report = Assert.Single(queue, r => r.TargetId == sent.Id);
        Assert.Equal("Send me your passport photo", report.Target!.Text);
        Assert.Equal("Owner", report.Target.Author!.DisplayName);
        Assert.Equal(ReportReason.Scam, report.Reason);

        Assert.Equal("NoteRequired", await (await mod.PostJsonAsync($"/api/admin/reports/{report.Id}/resolve", new { action = "remove" })).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostJsonAsync($"/api/admin/reports/{report.Id}/resolve", new { action = "remove", note = "Asking for ID documents breaks the community guidelines." })).StatusCode);

        var chat = await (await crew.Member.GetAsync($"/api/chats/card/{crew.Card.Id}")).ReadAsync<ChatDto>();
        Assert.DoesNotContain(chat.Messages, m => m.Id == sent.Id);
        var me = await (await crew.Owner.GetAsync("/api/auth/me")).ReadAsync<SessionDto>();
        Assert.Contains(factory.Emails.Sent, m => m.To == me.User!.Email && m.TextBody.Contains("Asking for ID documents", StringComparison.Ordinal));
        Assert.DoesNotContain(await (await mod.GetAsync("/api/admin/reports")).ReadAsync<List<ReportAdminDto>>(), r => r.TargetId == sent.Id);
    }

    [Fact]
    public async Task A_banned_person_is_signed_out_and_cant_sign_up_again()
    {
        var (bad, badAuth) = await factory.SignUpAsync("Bad");
        var email = badAuth.User!.Email;
        var mod = await ModeratorAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostJsonAsync($"/api/admin/users/{badAuth.User.Id}/ban", new { note = "Repeated harassment." })).StatusCode);

        Assert.Null((await (await bad.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);

        // Same inbox through a +tag, from a fresh browser.
        var at = email.IndexOf('@', StringComparison.Ordinal);
        var tagged = $"{email[..at]}+again{email[at..]}";
        Assert.Equal("AccountSuspended", await (await factory.CreateApiClient().PostJsonAsync("/api/auth/register", Registration(tagged))).ErrorCodeAsync());

        // A new address, but the same browser.
        Assert.Equal("AccountSuspended", await (await bad.PostJsonAsync("/api/auth/register", Registration(ApiHelpers.NewEmail("fresh")))).ErrorCodeAsync());

        await mod.PostAsync($"/api/admin/users/{badAuth.User.Id}/unban", null);
        Assert.Equal(HttpStatusCode.OK, (await bad.PostJsonAsync("/api/auth/register", Registration(ApiHelpers.NewEmail("fresh")))).StatusCode);
    }

    [Fact]
    public async Task Messages_are_rate_limited_per_person()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:MessagesPerMinute", "3"));
        var (owner, _) = await SignUpOnAsync(limited, "Chatty");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await owner.PostJsonAsync($"/api/chats/card/{card.Id}/messages", new { body = $"msg {i}" })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Theory]
    [InlineData("J.Doe+trips@GoogleMail.com", "jdoe@gmail.com")]
    [InlineData("anna+x@example.com", "anna@example.com")]
    [InlineData("a.b@example.com", "a.b@example.com")]
    public void Emails_are_compared_as_one_inbox(string email, string canonical) =>
        Assert.Equal(canonical, BanGuard.CanonicalEmail(email));

    private static async Task<(HttpClient Client, AuthResultDto Auth)> SignUpOnAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, string name)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add(CsrfMiddleware.HeaderName, "1");
        var res = await client.PostJsonAsync("/api/auth/register", new
        {
            email = ApiHelpers.NewEmail(name),
            password = "correct horse battery",
            displayName = name,
            fullName = $"{name} Example",
            dateOfBirth = "1995-05-05",
            countryCode = "IL",
            acceptTerms = true,
        });
        return (client, await res.ReadAsync<AuthResultDto>());
    }
}
