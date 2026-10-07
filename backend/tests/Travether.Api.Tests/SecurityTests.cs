using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Cards;
using Travether.Api.Chat;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

/// <summary>Regression tests for the pre-launch security review (docs/SECURITY_REVIEW.md).</summary>
[Collection(DatabaseTests.Name)]
public sealed class SecurityTests(PostgresFixture pg) : IAsyncLifetime
{
    private const string Password = "correct horse battery";
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private async Task<string> CodeAsync(HttpClient client, string email)
    {
        await client.PostJsonAsync("/api/auth/email/start", new { email });
        return factory.Emails.LatestCode(email);
    }

    [Fact]
    public async Task Proving_the_email_shuts_out_whoever_registered_it_with_a_password()
    {
        // An attacker registers the victim's address with a password the attacker knows.
        var (attacker, squatted) = await factory.SignUpAsync("Victim");
        var email = squatted.User!.Email;

        // The real owner later signs in with an emailed code.
        var owner = factory.CreateApiClient();
        var res = await (await owner.PostJsonAsync("/api/auth/email/verify", new { email, code = await CodeAsync(owner, email) })).ReadAsync<AuthResultDto>();
        Assert.Equal("signedIn", res.Status);
        Assert.False(res.User!.HasPassword);

        // The attacker's session and password no longer work; the owner's session does.
        Assert.Equal(HttpStatusCode.Unauthorized, (await attacker.GetAsync("/api/me")).StatusCode);
        Assert.Equal("InvalidCredentials", await (await factory.CreateApiClient().PostJsonAsync("/api/auth/login", new { email, password = Password })).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/me")).StatusCode);

        // Once verified, a later code sign-in leaves a password the owner set alone.
        await owner.PostJsonAsync("/api/auth/password/forgot", new { email });
        await owner.PostJsonAsync("/api/auth/password/reset", new { email, code = factory.Emails.LatestCode(email), newPassword = "a new long password" });
        var again = factory.CreateApiClient();
        await again.PostJsonAsync("/api/auth/email/verify", new { email, code = await CodeAsync(again, email) });
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateApiClient().PostJsonAsync("/api/auth/login", new { email, password = "a new long password" })).StatusCode);
    }

    [Fact]
    public async Task Parallel_guesses_still_count_against_the_five_attempt_limit()
    {
        var client = factory.CreateApiClient();
        var email = ApiHelpers.NewEmail("Racer");
        var code = await CodeAsync(client, email);
        var wrong = code == "000000" ? "111111" : "000000";

        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostJsonAsync("/api/auth/email/verify", new { email, code = wrong })));

        await using var db = pg.CreateDbContext();
        Assert.Equal(5, (await db.LoginCodes.SingleAsync(c => c.Email == email)).Attempts);
        Assert.Equal("TooManyAttempts", await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code })).ErrorCodeAsync());
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void Rate_limits_group_ipv6_clients_by_their_64(string ip, string key) =>
        Assert.Equal(key, ClientKey.Of(System.Net.IPAddress.Parse(ip)));

    [Fact]
    public async Task Responses_forbid_framing_and_sniffing()
    {
        var res = await factory.CreateClient().GetAsync("/api/health");
        Assert.Equal("DENY", res.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("strict-origin-when-cross-origin", res.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task An_open_chat_connection_closes_once_its_session_ends()
    {
        var (client, auth) = await factory.SignUpAsync("Socket");
        var (_, other) = await factory.SignUpAsync("Other");
        var sessions = new HubSessions();
        var mine = new FakeConnection(auth.User!.Id, 0);
        var theirs = new FakeConnection(other.User!.Id, 0);
        sessions.Add(mine);
        sessions.Add(theirs);

        await client.PostAsync("/api/auth/logout-all", null);
        await using (var db = pg.CreateDbContext())
        {
            await sessions.SweepAsync(db, default);
        }

        Assert.True(mine.Aborted);
        Assert.False(theirs.Aborted);
        Assert.Equal(1, sessions.Count);
    }

    [Fact]
    public async Task A_card_member_blocked_by_the_host_cannot_join_or_be_added_to_the_hosts_plan()
    {
        var (host, hostAuth) = await factory.SignUpAsync("Host");
        var card = await (await host.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await AddMemberAsync(card.Id, memberAuth.User!.Id);
        var plan = await (await host.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();

        await host.PostAsync($"/api/users/{memberAuth.User.Id}/block", null);

        Assert.Equal(HttpStatusCode.NotFound, (await member.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/plans/{plan.Id}")).StatusCode);
        var added = await member.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan(participantIds: [hostAuth.User!.Id]));
        Assert.Equal("NotCardMembers", await added.ErrorCodeAsync());
    }

    [Fact]
    public async Task Removing_a_member_cancels_the_upcoming_plans_they_host_in_that_card()
    {
        var (owner, _) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await AddMemberAsync(card.Id, memberAuth.User!.Id);
        var plan = await (await member.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);

        Assert.True((await owner.DeleteAsync($"/api/cards/{card.Id}/members/{memberAuth.User.Id}")).IsSuccessStatusCode);

        await using var db = pg.CreateDbContext();
        Assert.Equal(PlanStatus.Cancelled, (await db.ActivityPlans.SingleAsync(p => p.Id == plan.Id)).Status);
        Assert.Equal("PlanClosed", await (await member.PatchJsonAsync($"/api/plans/{plan.Id}", new { title = "Moved" })).ErrorCodeAsync());
        var notes = await (await owner.GetAsync("/api/notifications")).ReadAsync<NotificationPageDto>();
        Assert.Contains(notes.Items, n => n.Type == NotificationTypes.PlanCancelled);
    }

    [Fact]
    public async Task Deleting_an_account_removes_shared_phone_numbers_and_the_name_in_others_notifications()
    {
        var (owner, ownerAuth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var (leaver, leaverAuth) = await factory.SignUpAsync("Leaver");
        var leaverId = leaverAuth.User!.Id;
        await AddMemberAsync(card.Id, leaverId);
        await using (var db = pg.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == leaverId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Phone, "+66812345678"));
        }

        Assert.True((await leaver.PostJsonAsync($"/api/chats/card/{card.Id}/contact", new { kind = "whatsapp" })).IsSuccessStatusCode);
        await leaver.PostJsonAsync($"/api/chats/card/{card.Id}/messages", new { body = "See you at the gate" });
        await (await leaver.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();

        Assert.Equal(HttpStatusCode.NoContent, (await leaver.PostJsonAsync("/api/me/delete", new { password = Password })).StatusCode);

        await using var check = pg.CreateDbContext();
        var contact = await check.Messages.IgnoreQueryFilters().SingleAsync(m => m.SenderId == leaverId && m.Kind == MessageKind.ContactWhatsapp);
        Assert.Equal("", contact.Body);
        Assert.NotNull(contact.HiddenAt);
        var payloads = await check.Notifications.Where(n => n.UserId == ownerAuth.User!.Id).Select(n => n.Payload).ToListAsync();
        Assert.NotEmpty(payloads);
        Assert.All(payloads, p =>
        {
            Assert.DoesNotContain("Leaver", p, StringComparison.Ordinal);
            Assert.DoesNotContain("See you", p, StringComparison.Ordinal);
        });

        var export = await (await owner.GetAsync("/api/me/export")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(NotificationTypes.ChatMessage, export, StringComparison.Ordinal);
    }

    private async Task AddMemberAsync(Guid cardId, Guid userId)
    {
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = cardId, UserId = userId, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private sealed class FakeConnection(Guid userId, int sessionVersion) : HubCallerContext
    {
        public bool Aborted { get; private set; }

        public override string ConnectionId { get; } = Guid.NewGuid().ToString("N");

        public override string? UserIdentifier => userId.ToString();

        public override ClaimsPrincipal? User { get; } = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(CurrentUser.SessionVersionClaim, sessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))], "test"));

        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

        public override IFeatureCollection Features { get; } = new FeatureCollection();

        public override CancellationToken ConnectionAborted => default;

        public override void Abort() => Aborted = true;
    }
}
