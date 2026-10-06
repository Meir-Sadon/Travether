using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Travether.Api.Cards;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Plans;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class NotificationTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Traveler(HttpClient Client, Guid Id, string Email, CardDto Card);

    /// <summary>A browser's push subscription: the private key stays here so tests can read what was sent.</summary>
    private sealed class Device : IDisposable
    {
        public ECDiffieHellman Key { get; } = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        public byte[] Auth { get; } = RandomNumberGenerator.GetBytes(16);
        public string Endpoint { get; } = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}";

        public byte[] PublicKey
        {
            get
            {
                var p = Key.ExportParameters(false);
                return [0x04, .. p.Q.X!, .. p.Q.Y!];
            }
        }

        public object Subscription(string? timeZone = null) => new { endpoint = Endpoint, p256dh = WebEncoders.Base64UrlEncode(PublicKey), auth = WebEncoders.Base64UrlEncode(Auth), timeZone };

        /// <summary>The receiving side of RFC 8291.</summary>
        public PushMessage Decrypt(byte[] body)
        {
            var salt = body[..16];
            Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
            var idLength = body[20];
            var serverPublic = body[21..(21 + idLength)];
            using var server = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
            });
            var shared = Key.DeriveRawSecretAgreement(server.PublicKey);
            var (key, nonce) = PushEncryption.DeriveKeys(shared, Auth, PublicKey, serverPublic, salt);
            var cipher = body[(21 + idLength)..];
            var plain = new byte[cipher.Length - 16];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher[..^16], cipher[^16..], plain);
            Assert.Equal(2, plain[^1]);
            return JsonSerializer.Deserialize<PushMessage>(plain.AsSpan(0, plain.Length - 1), ApiHelpers.Json)!;
        }

        public void Dispose() => Key.Dispose();
    }

    private async Task<Traveler> TravelerAsync(string name)
    {
        var (client, auth) = await factory.SignUpAsync(name);
        var card = await (await client.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        return new Traveler(client, auth.User!.Id, auth.User.Email, card);
    }

    private static async Task<PlanDto> PlanAsync(Traveler host)
    {
        var res = await host.Client.PostJsonAsync($"/api/cards/{host.Card.Id}/plans", PlanTests.NewPlan());
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return await res.ReadAsync<PlanDto>();
    }

    private async Task DeliverAsync()
    {
        using var scope = factory.Services.CreateScope();
        var delivery = scope.ServiceProvider.GetRequiredService<NotificationDelivery>();
        while (await delivery.DeliverPendingAsync(default) > 0)
        {
        }
    }

    private async Task<T> JobAsync<T>(Func<NotificationJobs, Task<T>> run)
    {
        using var scope = factory.Services.CreateScope();
        return await run(scope.ServiceProvider.GetRequiredService<NotificationJobs>());
    }

    private static async Task<NotificationPageDto> ListAsync(HttpClient client) =>
        await (await client.GetAsync("/api/notifications")).ReadAsync<NotificationPageDto>();

    private static async Task SettingsAsync(HttpClient client, Func<NotificationSettingsDto, NotificationSettingsDto> change)
    {
        var current = await (await client.GetAsync("/api/notifications/settings")).ReadAsync<NotificationSettingsDto>();
        var res = await client.PutJsonAsync("/api/notifications/settings", change(current));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    private static NotificationSettingsDto NoQuietHours(NotificationSettingsDto s) => s with { QuietFrom = null, QuietTo = null };

    [Fact]
    public async Task A_join_request_reaches_the_host_in_app_by_push_and_by_email()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        using var phone = new Device();
        Assert.Equal(HttpStatusCode.NoContent, (await host.Client.PostJsonAsync("/api/push/subscriptions", phone.Subscription("Asia/Bangkok"))).StatusCode);
        await SettingsAsync(host.Client, NoQuietHours);
        var (solo, _) = await factory.SignUpAsync("Solo");

        await solo.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { message = "Hi!" });

        var page = await ListAsync(host.Client);
        var item = Assert.Single(page.Items);
        Assert.Equal(NotificationTypes.PlanRequest, item.Type);
        Assert.Equal("Solo", item.Payload.Actor);
        Assert.Equal(plan.Title, item.Payload.Subject);
        Assert.Equal(1, page.Unread);

        await DeliverAsync();

        var push = Assert.Single(factory.Push.To(phone.Endpoint));
        Assert.StartsWith("vapid t=", push.Authorization);
        var message = phone.Decrypt(push.Body);
        Assert.Equal("New join request", message.Title);
        Assert.Equal($"Solo asked to join {plan.Title}.", message.Body);
        Assert.Equal("/inbox", message.Url);
        Assert.Contains(factory.Emails.Sent, m => m.To == host.Email && m.Subject == "New join request" && m.TextBody.Contains("/inbox", StringComparison.Ordinal));

        await host.Client.PostJsonAsync("/api/notifications/read", new { ids = (Guid[]?)null });
        Assert.Equal(0, (await (await host.Client.GetAsync("/api/notifications/unread")).ReadAsync<UnreadDto>()).Count);

        // Approval notifies the requester.
        var request = (await (await host.Client.GetAsync($"/api/plans/{plan.Id}/requests")).ReadAsync<List<PlanRequestDto>>())[0];
        await host.Client.PostAsync($"/api/plan-requests/{request.Id}/approve", null);
        Assert.Equal(NotificationTypes.PlanRequestApproved, Assert.Single((await ListAsync(solo)).Items).Type);
    }

    [Fact]
    public async Task A_category_turned_off_stays_in_app_only_and_quiet_hours_hold_back_push()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        using var phone = new Device();
        await host.Client.PostJsonAsync("/api/push/subscriptions", phone.Subscription());
        await SettingsAsync(host.Client, s => NoQuietHours(s) with { Requests = false });
        var (first, _) = await factory.SignUpAsync("First");

        await first.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { });
        await DeliverAsync();

        Assert.Single((await ListAsync(host.Client)).Items);
        Assert.Empty(factory.Push.To(phone.Endpoint));
        Assert.DoesNotContain(factory.Emails.Sent, m => m.To == host.Email && m.Subject == "New join request");

        // Requests back on, but now inside quiet hours (UTC): email only.
        var now = TimeOnly.FromDateTime(DateTime.UtcNow);
        await SettingsAsync(host.Client, s => s with { Requests = true, QuietFrom = now.AddHours(-1), QuietTo = now.AddHours(1), TimeZone = "UTC" });
        var (second, _) = await factory.SignUpAsync("Second");
        await second.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { });
        await DeliverAsync();

        Assert.Empty(factory.Push.To(phone.Endpoint));
        Assert.Contains(factory.Emails.Sent, m => m.To == host.Email && m.TextBody.StartsWith("Second asked", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Chat_pushes_are_batched_per_conversation_and_stay_out_of_the_list()
    {
        var host = await TravelerAsync("Host");
        var (friend, friendAuth) = await factory.SignUpAsync("Friend");
        await using (var db = pg.CreateDbContext())
        {
            db.CardMembers.Add(new CardMember { CardId = host.Card.Id, UserId = friendAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        using var phone = new Device();
        await friend.PostJsonAsync("/api/push/subscriptions", phone.Subscription());
        await SettingsAsync(friend, NoQuietHours);

        await host.Client.PostJsonAsync($"/api/chats/card/{host.Card.Id}/messages", new { body = "Who's up for khao soi?" });
        await host.Client.PostJsonAsync($"/api/chats/card/{host.Card.Id}/messages", new { body = "7pm?" });
        await DeliverAsync();

        var message = phone.Decrypt(Assert.Single(factory.Push.To(phone.Endpoint)).Body);
        Assert.Equal(host.Card.Name, message.Title);
        Assert.Equal("Host: Who's up for khao soi?", message.Body);
        Assert.Equal($"/inbox/card-{host.Card.Id}", message.Url);
        Assert.Empty((await ListAsync(friend)).Items);
    }

    [Fact]
    public async Task A_subscription_the_push_service_forgot_is_removed()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        using var phone = new Device();
        await host.Client.PostJsonAsync("/api/push/subscriptions", phone.Subscription());
        await SettingsAsync(host.Client, NoQuietHours);
        factory.Push.Answers[phone.Endpoint] = HttpStatusCode.Gone;
        var (solo, _) = await factory.SignUpAsync("Solo");

        await solo.PostJsonAsync($"/api/plans/{plan.Id}/requests", new { });
        await DeliverAsync();

        await using var db = pg.CreateDbContext();
        Assert.False(await db.PushSubscriptions.AnyAsync(s => s.Endpoint == phone.Endpoint));
    }

    [Theory]
    [InlineData("https://evil.example.com/push", "PushEndpointNotAllowed")]
    [InlineData("http://fcm.googleapis.com/fcm/send/x", "PushEndpointNotAllowed")]
    [InlineData("https://169.254.169.254/latest", "PushEndpointNotAllowed")]
    public async Task Only_known_push_services_are_accepted(string endpoint, string code)
    {
        var (client, _) = await factory.SignUpAsync("Pushy");
        using var phone = new Device();

        var res = await client.PostJsonAsync("/api/push/subscriptions", new { endpoint, p256dh = WebEncoders.Base64UrlEncode(phone.PublicKey), auth = WebEncoders.Base64UrlEncode(phone.Auth) });

        Assert.Equal(code, await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Malformed_push_keys_and_unknown_time_zones_are_refused()
    {
        var (client, _) = await factory.SignUpAsync("Pushy");

        var keys = await client.PostJsonAsync("/api/push/subscriptions", new { endpoint = "https://fcm.googleapis.com/fcm/send/x", p256dh = "abc", auth = "def" });
        var zone = await client.PutJsonAsync("/api/notifications/settings", new NotificationSettingsDto(true, true, true, true, true, true, null, null, "Mars/Olympus"));

        Assert.Equal("InvalidPushKeys", await keys.ErrorCodeAsync());
        Assert.Equal("InvalidTimeZone", await zone.ErrorCodeAsync());
        Assert.True((await (await factory.CreateApiClient().GetAsync("/api/push/key")).ReadAsync<PushKeyDto>()).Enabled);
    }

    [Fact]
    public async Task Participants_get_one_reminder_per_window()
    {
        var host = await TravelerAsync("Host");
        var plan = await PlanAsync(host);
        await using (var db = pg.CreateDbContext())
        {
            await db.ActivityPlans.Where(p => p.Id == plan.Id).ExecuteUpdateAsync(u => u
                .SetProperty(p => p.StartsAt, DateTimeOffset.UtcNow.AddHours(20))
                .SetProperty(p => p.CreatedAt, DateTimeOffset.UtcNow.AddDays(-2)));
        }

        await JobAsync(j => j.RemindAsync(default));
        await JobAsync(j => j.RemindAsync(default));

        var reminder = Assert.Single((await ListAsync(host.Client)).Items);
        Assert.Equal(NotificationTypes.PlanReminder, reminder.Type);
        Assert.Equal(24, reminder.Payload.Count);
    }

    [Fact]
    public async Task The_daily_digest_counts_new_plans_near_a_trip_once_a_day()
    {
        var host = await TravelerAsync("Host");
        await PlanAsync(host);
        var traveler = await TravelerAsync("Traveler");
        await using (var db = pg.CreateDbContext())
        {
            var area = PlanRules.ToPoint(new LatLng(18.79, 98.99));
            await db.VacationCards.Where(c => c.Id == traveler.Card.Id).ExecuteUpdateAsync(u => u.SetProperty(c => c.Area, area));
        }

        await JobAsync(j => j.DigestAsync(default));
        await JobAsync(j => j.DigestAsync(default));

        var digests = (await ListAsync(traveler.Client)).Items.Where(n => n.Type == NotificationTypes.MatchesDigest).ToList();
        var digest = Assert.Single(digests);
        Assert.True(digest.Payload.Count >= 1);
        Assert.Equal(traveler.Card.Name, digest.Payload.Subject);
    }
}
