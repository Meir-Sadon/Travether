using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Travether.Api.Auth;
using Travether.Api.Cards;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Plans;
using Travether.Api.Privacy;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class PrivacyTests(PostgresFixture pg) : IAsyncLifetime
{
    private const string Password = "correct horse battery";
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Crew(HttpClient Owner, Account OwnerUser, HttpClient Member, Guid MemberId, CardDto Card);

    private sealed record Account(Guid Id, string Email);

    private async Task<Crew> CrewAsync()
    {
        var (owner, ownerAuth) = await factory.SignUpAsync("Owner");
        var card = await (await owner.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var (member, memberAuth) = await factory.SignUpAsync("Member");
        await using var db = pg.CreateDbContext();
        db.CardMembers.Add(new CardMember { CardId = card.Id, UserId = memberAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new Crew(owner, new Account(ownerAuth.User!.Id, ownerAuth.User.Email), member, memberAuth.User.Id, card);
    }

    private static async Task<PrivacyDto> PrivacyAsync(HttpClient client) =>
        await (await client.GetAsync("/api/me/privacy")).ReadAsync<PrivacyDto>();

    [Fact]
    public async Task The_export_holds_my_data_as_a_json_download()
    {
        var c = await CrewAsync();
        await c.Owner.PostJsonAsync($"/api/chats/card/{c.Card.Id}/messages", new { body = "Khao soi at 7?" });
        await c.Owner.PostAsync($"/api/users/{c.MemberId}/block", null);

        var res = await c.Owner.GetAsync("/api/me/export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/json", res.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("travether-data-", res.Content.Headers.ContentDisposition!.FileName!.Trim('"'), StringComparison.Ordinal);

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(c.OwnerUser.Email, root.GetProperty("account").GetProperty("email").GetString());
        Assert.Equal("Owner Example", root.GetProperty("account").GetProperty("fullName").GetString());
        Assert.Equal(c.Card.Id, root.GetProperty("vacationCards")[0].GetProperty("cardId").GetGuid());
        Assert.Equal("owner", root.GetProperty("vacationCards")[0].GetProperty("role").GetString());
        Assert.Equal("Khao soi at 7?", root.GetProperty("messagesSent")[0].GetProperty("body").GetString());
        Assert.Equal(c.MemberId, root.GetProperty("blocked")[0].GetProperty("userId").GetGuid());
        Assert.Equal(3, root.GetProperty("consents").GetArrayLength());

        // The member's own details are not part of the owner's export.
        Assert.DoesNotContain("Member Example", root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_legal_documents_ask_for_consent_again()
    {
        var (client, auth) = await factory.SignUpAsync("Consent");
        Assert.False((await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).NeedsConsent);

        await using (var db = pg.CreateDbContext())
        {
            await db.Consents.Where(c => c.UserId == auth.User!.Id).ExecuteUpdateAsync(u => u.SetProperty(c => c.Version, "2020-01-01"));
        }

        Assert.True((await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).NeedsConsent);
        var after = await (await client.PostAsync("/api/me/consents/legal", null)).ReadAsync<PrivacyDto>();
        Assert.False(after.NeedsConsent);
        Assert.Equal(6, after.History.Count);
        Assert.False((await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).NeedsConsent);
    }

    [Fact]
    public async Task Analytics_is_off_until_granted_and_can_be_withdrawn()
    {
        var (client, _) = await factory.SignUpAsync("Analytics");
        Assert.False((await PrivacyAsync(client)).Analytics);

        var on = await (await client.PutJsonAsync("/api/me/consents", new { kind = "analytics", granted = true })).ReadAsync<PrivacyDto>();
        Assert.True(on.Analytics);
        var off = await (await client.PutJsonAsync("/api/me/consents", new { kind = "analytics", granted = false })).ReadAsync<PrivacyDto>();
        Assert.False(off.Analytics);
        Assert.NotNull(Assert.Single(off.History, h => h.Kind == ConsentKind.Analytics).WithdrawnAt);

        Assert.Equal("ConsentNotOptional", await (await client.PutJsonAsync("/api/me/consents", new { kind = "terms", granted = false })).ErrorCodeAsync());
    }

    [Fact]
    public async Task Deleting_hands_the_card_over_cancels_hosted_plans_and_scrubs_the_account()
    {
        var c = await CrewAsync();
        var plan = await (await c.Owner.PostJsonAsync($"/api/cards/{c.Card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();
        Assert.Equal(HttpStatusCode.OK, (await c.Member.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);

        Assert.Equal("ConfirmationRequired", await (await c.Owner.PostJsonAsync("/api/me/delete", new { })).ErrorCodeAsync());
        Assert.Equal("WrongPassword", await (await c.Owner.PostJsonAsync("/api/me/delete", new { password = "not it at all" })).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await c.Owner.PostJsonAsync("/api/me/delete", new { password = Password })).StatusCode);

        // Signed out everywhere.
        Assert.Null((await (await c.Owner.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Owner.GetAsync("/api/me")).StatusCode);
        Assert.Contains(factory.Emails.Sent, m => m.To == c.OwnerUser.Email && m.Subject.Contains("deleted", StringComparison.Ordinal));

        // The member now owns the card; the plan the owner hosted is cancelled and they were told.
        var card = await (await c.Member.GetAsync($"/api/cards/{c.Card.Id}")).ReadAsync<CardDto>();
        Assert.Equal("owner", card.Access);
        await using (var db = pg.CreateDbContext())
        {
            Assert.Equal(PlanStatus.Cancelled, (await db.ActivityPlans.SingleAsync(p => p.Id == plan.Id)).Status);
            var user = await db.Users.SingleAsync(u => u.Id == c.OwnerUser.Id);
            Assert.NotNull(user.DeletedAt);
            Assert.Equal(PrivacyRules.DeletedEmail(user.Id), user.Email);
            Assert.Equal("", user.FullName);
            Assert.Null(user.PasswordHash);
            Assert.False(await db.Consents.AnyAsync(x => x.UserId == user.Id && x.WithdrawnAt == null));
            Assert.Equal(MembershipStatus.Left, (await db.CardMembers.IgnoreQueryFilters().SingleAsync(m => m.CardId == c.Card.Id && m.UserId == user.Id)).Status);
        }

        var notes = await (await c.Member.GetAsync("/api/notifications")).ReadAsync<NotificationPageDto>();
        Assert.Contains(notes.Items, n => n.Type == NotificationTypes.PlanCancelled);

        // The address is free for a new account.
        var again = factory.CreateApiClient();
        var res = await again.PostJsonAsync("/api/auth/register", new
        {
            email = c.OwnerUser.Email,
            password = Password,
            displayName = "Back",
            fullName = "Back Again",
            dateOfBirth = "1995-05-05",
            countryCode = "IL",
            acceptTerms = true,
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task An_emailed_code_also_confirms_and_an_unshared_card_goes_with_the_account()
    {
        var (client, auth) = await factory.SignUpAsync("Solo");
        var card = await (await client.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/me/delete/code", null)).StatusCode);
        Assert.Equal("InvalidCode", await (await client.PostJsonAsync("/api/me/delete", new { code = "000000" })).ErrorCodeAsync());
        var code = factory.Emails.LatestCode(auth.User!.Email);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostJsonAsync("/api/me/delete", new { code })).StatusCode);

        await using var db = pg.CreateDbContext();
        Assert.NotNull((await db.VacationCards.IgnoreQueryFilters().SingleAsync(x => x.Id == card.Id)).DeletedAt);
        Assert.False(await db.LoginCodes.AnyAsync(x => x.Email == auth.User.Email));
    }

    [Fact]
    public async Task Retention_removes_old_notifications_codes_and_devices()
    {
        var (_, auth) = await factory.SignUpAsync("Old");
        var id = auth.User!.Id;
        var longAgo = DateTimeOffset.UtcNow.AddDays(-200);
        await using (var db = pg.CreateDbContext())
        {
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = id, Type = NotificationTypes.PlanJoined, Payload = "{\"url\":\"/\"}", CreatedAt = longAgo });
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = id, Type = NotificationTypes.PlanJoined, Payload = "{\"url\":\"/\"}", CreatedAt = DateTimeOffset.UtcNow });
            db.LoginCodes.Add(new LoginCode { Id = Guid.NewGuid(), Email = "old@example.com", Purpose = LoginCodePurpose.SignIn, CodeHash = "x", CreatedAt = longAgo, ExpiresAt = longAgo });
            await db.SaveChangesAsync();
            await db.UserDevices.Where(d => d.UserId == id).ExecuteUpdateAsync(u => u.SetProperty(d => d.LastSeenAt, longAgo));
        }

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PrivacyService>().ApplyRetentionAsync(default);
        }

        await using var check = pg.CreateDbContext();
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == id));
        Assert.False(await check.LoginCodes.AnyAsync(x => x.Email == "old@example.com"));
        Assert.False(await check.UserDevices.AnyAsync(d => d.UserId == id));
    }
}
