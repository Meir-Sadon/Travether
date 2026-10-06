using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class ProfileTests(PostgresFixture pg) : IAsyncLifetime
{
    // Smallest valid PNG: 1×1 transparent pixel.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private static MultipartFormDataContent Upload(byte[] bytes, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "photo.png" } };
    }

    [Fact]
    public async Task Profile_strength_grows_as_the_profile_is_filled_in()
    {
        var (client, auth) = await factory.SignUpAsync("Strong");
        Assert.Equal(0, auth.User!.Strength.Percent);
        Assert.Equal("contactVerified", auth.User.Strength.Missing[0]);

        var me = await (await client.PatchJsonAsync("/api/me", new { bio = "Early riser.", interests = new[] { "hiking" }, languages = new[] { "en" } })).ReadAsync<MeDto>();
        Assert.Equal(45, me.Strength.Percent);
        Assert.Equal(["contactVerified", "photo"], me.Strength.Missing);

        await client.PostJsonAsync("/api/auth/email/confirm", new { code = factory.Emails.LatestCode(me.Email) });
        me = await (await client.PostAsync("/api/me/photo", Upload(Png))).ReadAsync<MeDto>();
        Assert.Equal(100, me.Strength.Percent);
        Assert.Empty(me.Strength.Missing);
    }

    [Fact]
    public async Task Photo_upload_checks_the_bytes_and_replaces_the_old_photo()
    {
        var (client, _) = await factory.SignUpAsync("Photo");

        var first = await (await client.PostAsync("/api/me/photo", Upload(Png))).ReadAsync<MeDto>();
        Assert.StartsWith("/uploads/avatars/", first.PhotoUrl);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(first.PhotoUrl)).StatusCode);

        var second = await (await client.PostAsync("/api/me/photo", Upload(Png))).ReadAsync<MeDto>();
        Assert.NotEqual(first.PhotoUrl, second.PhotoUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(first.PhotoUrl)).StatusCode);

        var fake = await client.PostAsync("/api/me/photo", Upload("<svg onload=alert(1)>"u8.ToArray()));
        Assert.Equal("UnsupportedImage", await fake.ErrorCodeAsync());

        var removed = await (await client.DeleteAsync("/api/me/photo")).ReadAsync<MeDto>();
        Assert.Null(removed.PhotoUrl);
    }

    [Fact]
    public async Task Phone_is_e164_and_can_be_cleared()
    {
        var (client, _) = await factory.SignUpAsync("Phone");

        var set = await (await client.PatchJsonAsync("/api/me", new { phone = "+972501234567" })).ReadAsync<MeDto>();
        var bad = await client.PatchJsonAsync("/api/me", new { phone = "050-123" });
        var cleared = await (await client.PatchJsonAsync("/api/me", new { phone = "" })).ReadAsync<MeDto>();

        Assert.Equal("+972501234567", set.Phone);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Null(cleared.Phone);
    }

    [Fact]
    public async Task Public_profile_hides_private_fields_and_shows_full_name_to_co_participants()
    {
        var (aliceClient, alice) = await factory.SignUpAsync("Alice");
        var (bobClient, bob) = await factory.SignUpAsync("Bob");
        var (_, cleo) = await factory.SignUpAsync("Cleo");
        await aliceClient.PatchJsonAsync("/api/me", new { phone = "+972501234567" });

        // Alice and Bob share a trip; Cleo doesn't.
        await using (var db = pg.CreateDbContext())
        {
            var card = Scenario.NewCard(await db.Users.FirstAsync(u => u.Id == alice.User!.Id), CardVisibility.Public);
            card.Members.Add(new CardMember { UserId = bob.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active });
            db.VacationCards.Add(card);
            await db.SaveChangesAsync();
        }

        var visitorView = await (await factory.CreateApiClient().GetAsync($"/api/users/{alice.User!.Id}")).ReadAsync<PublicProfileDto>();
        var bobView = await (await bobClient.GetAsync($"/api/users/{alice.User.Id}")).ReadAsync<PublicProfileDto>();
        var raw = await (await bobClient.GetAsync($"/api/users/{alice.User.Id}")).Content.ReadAsStringAsync();

        Assert.Equal("Alice", visitorView.DisplayName);
        Assert.Equal(alice.User.Age, visitorView.Age);
        Assert.Null(visitorView.FullName);
        Assert.Null(visitorView.Email);
        Assert.Equal("Alice Example", bobView.FullName);
        Assert.Null(bobView.Email);
        Assert.DoesNotContain("+972501234567", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("1995-05-05", raw, StringComparison.Ordinal);
        Assert.NotEqual(cleo.User!.Id, bobView.Id);
    }

    [Fact]
    public async Task Blocked_and_missing_profiles_are_404()
    {
        var (aliceClient, alice) = await factory.SignUpAsync("Alice");
        var (_, mallory) = await factory.SignUpAsync("Mallory");
        await using (var db = pg.CreateDbContext())
        {
            db.Blocks.Add(new Block { BlockerId = mallory.User!.Id, BlockedId = alice.User!.Id });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await aliceClient.GetAsync($"/api/users/{mallory.User.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aliceClient.GetAsync($"/api/users/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Rating_average_appears_only_from_three_published_reviews()
    {
        var (_, target) = await factory.SignUpAsync("Rated");
        var targetId = target.User!.Id;
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);

        async Task AddReviewAsync(User reviewer, int stars, bool published)
        {
            db.Reviews.Add(new Review
            {
                Id = Guid.NewGuid(),
                PlanId = s.Hike.Id,
                ReviewerId = reviewer.Id,
                RevieweeId = targetId,
                Stars = stars,
                PublishedAt = published ? DateTimeOffset.UtcNow : null,
            });
            await db.SaveChangesAsync();
        }

        await AddReviewAsync(s.Alice, 5, published: true);
        await AddReviewAsync(s.Bob, 4, published: true);
        await AddReviewAsync(s.Cleo, 1, published: false); // still hidden: double-blind
        var two = await (await factory.CreateApiClient().GetAsync($"/api/users/{targetId}")).ReadAsync<PublicProfileDto>();
        Assert.Equal(new RatingSummary(null, 2), two.Rating);

        await AddReviewAsync(s.Olga, 3, published: true);
        var three = await (await factory.CreateApiClient().GetAsync($"/api/users/{targetId}")).ReadAsync<PublicProfileDto>();
        Assert.Equal(new RatingSummary(4.0, 3), three.Rating);
    }
}
