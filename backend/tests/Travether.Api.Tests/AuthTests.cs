using System.Net;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class AuthTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private static object Profile(string? email = null, string? password = null, string? signupToken = null, string dob = "1990-01-31", bool terms = true) => new
    {
        signupToken,
        email,
        password,
        displayName = "Noa",
        fullName = "Noa Levi",
        dateOfBirth = dob,
        countryCode = "IL",
        acceptTerms = terms,
    };

    [Fact]
    public async Task Password_sign_up_creates_the_account_records_consent_and_signs_in()
    {
        var client = factory.CreateApiClient();
        var email = ApiHelpers.NewEmail("Noa");

        var res = await client.PostJsonAsync("/api/auth/register", Profile(email.ToUpperInvariant(), "correct horse battery"));
        var auth = await res.ReadAsync<AuthResultDto>();

        Assert.Equal("signedIn", auth.Status);
        Assert.Equal(email, auth.User!.Email); // stored lower-case
        Assert.True(auth.User.HasPassword);
        Assert.Empty(auth.User.Badges); // not verified until the emailed code is entered

        var me = await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>();
        Assert.Equal(auth.User.Id, me.User!.Id);

        await using var db = pg.CreateDbContext();
        var consents = await db.Consents.Where(c => c.UserId == auth.User.Id).Select(c => c.Kind).ToListAsync();
        Assert.Equal([ConsentKind.Terms, ConsentKind.PrivacyPolicy, ConsentKind.CommunityGuidelines], consents.Order());

        // A confirmation code was emailed; entering it earns the contact-verified badge.
        var code = factory.Emails.LatestCode(email);
        var confirmed = await (await client.PostJsonAsync("/api/auth/email/confirm", new { code })).ReadAsync<MeDto>();
        Assert.Equal(["contactVerified"], confirmed.Badges);
    }

    [Theory]
    [InlineData("2010-01-01", true, "Underage")]
    [InlineData("1990-01-01", false, "TermsRequired")]
    public async Task Sign_up_requires_18_plus_and_accepted_terms(string dob, bool terms, string expected)
    {
        var client = factory.CreateApiClient();

        var res = await client.PostJsonAsync("/api/auth/register", Profile(ApiHelpers.NewEmail("Kid"), "correct horse battery", dob: dob, terms: terms));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal(expected, await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Turning_18_today_is_old_enough()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var client = factory.CreateApiClient();

        var res = await client.PostJsonAsync("/api/auth/register", Profile(ApiHelpers.NewEmail("Birthday"), "correct horse battery", dob: today.AddYears(-18).ToString("O", System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task An_email_can_only_have_one_account()
    {
        var email = ApiHelpers.NewEmail("Dup");
        await factory.CreateApiClient().PostJsonAsync("/api/auth/register", Profile(email, "correct horse battery"));

        var res = await factory.CreateApiClient().PostJsonAsync("/api/auth/register", Profile(email, "another password"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("EmailTaken", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Password_login_checks_the_password()
    {
        var email = ApiHelpers.NewEmail("Login");
        await factory.CreateApiClient().PostJsonAsync("/api/auth/register", Profile(email, "correct horse battery"));
        var client = factory.CreateApiClient();

        var wrong = await client.PostJsonAsync("/api/auth/login", new { email, password = "wrong password" });
        var unknown = await client.PostJsonAsync("/api/auth/login", new { email = ApiHelpers.NewEmail("Nobody"), password = "whatever123" });
        var right = await client.PostJsonAsync("/api/auth/login", new { email = email.ToUpperInvariant(), password = "correct horse battery" });

        Assert.Equal("InvalidCredentials", await wrong.ErrorCodeAsync());
        Assert.Equal("InvalidCredentials", await unknown.ErrorCodeAsync()); // same answer: no account enumeration
        Assert.Equal("signedIn", (await right.ReadAsync<AuthResultDto>()).Status);
    }

    [Fact]
    public async Task Email_code_signs_up_a_new_traveler_with_a_verified_email()
    {
        var client = factory.CreateApiClient();
        var email = ApiHelpers.NewEmail("Code");

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostJsonAsync("/api/auth/email/start", new { email })).StatusCode);
        var verify = await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code = factory.Emails.LatestCode(email) })).ReadAsync<AuthResultDto>();

        Assert.Equal("needsProfile", verify.Status);
        Assert.Null((await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User); // no account yet

        var registered = await (await client.PostJsonAsync("/api/auth/register", Profile(signupToken: verify.SignupToken))).ReadAsync<AuthResultDto>();
        Assert.Equal(email, registered.User!.Email);
        Assert.Equal(["contactVerified"], registered.User.Badges);
        Assert.False(registered.User.HasPassword);

        // Next time the same code flow signs straight in.
        var again = factory.CreateApiClient();
        await again.PostJsonAsync("/api/auth/email/start", new { email });
        var signedIn = await (await again.PostJsonAsync("/api/auth/email/verify", new { email, code = factory.Emails.LatestCode(email) })).ReadAsync<AuthResultDto>();
        Assert.Equal("signedIn", signedIn.Status);
        Assert.Equal(registered.User.Id, signedIn.User!.Id);
    }

    [Fact]
    public async Task A_code_works_once_and_dies_after_five_wrong_guesses()
    {
        var client = factory.CreateApiClient();
        var email = ApiHelpers.NewEmail("Guess");
        await client.PostJsonAsync("/api/auth/email/start", new { email });
        var code = factory.Emails.LatestCode(email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal("InvalidCode", await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code = wrong })).ErrorCodeAsync());
        }

        Assert.Equal("TooManyAttempts", await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code = wrong })).ErrorCodeAsync());
        Assert.Equal("TooManyAttempts", await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code })).ErrorCodeAsync());

        await client.PostJsonAsync("/api/auth/email/start", new { email });
        var fresh = factory.Emails.LatestCode(email);
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync("/api/auth/email/verify", new { email, code = fresh })).StatusCode);
        Assert.Equal("InvalidCode", await (await client.PostJsonAsync("/api/auth/email/verify", new { email, code = fresh })).ErrorCodeAsync());
    }

    [Fact]
    public async Task Only_five_codes_per_hour_go_to_one_address()
    {
        var client = factory.CreateApiClient();
        var email = ApiHelpers.NewEmail("Flood");

        for (var i = 0; i < LoginCodeService.MaxCodesPerHour; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostJsonAsync("/api/auth/email/start", new { email })).StatusCode);
        }

        var res = await client.PostJsonAsync("/api/auth/email/start", new { email });
        Assert.Equal(HttpStatusCode.TooManyRequests, res.StatusCode);
        Assert.Equal("TooManyCodes", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Google_sign_in_creates_then_reuses_the_linked_account()
    {
        var email = ApiHelpers.NewEmail("G");
        var identity = new ExternalIdentity(ExternalProvider.Google, Guid.NewGuid().ToString(), email, true, "Gal");
        var client = factory.CreateApiClient();

        var first = await (await client.PostJsonAsync("/api/auth/external", new { provider = "google", idToken = factory.External.Issue(identity) })).ReadAsync<AuthResultDto>();
        Assert.Equal("needsProfile", first.Status);
        Assert.Equal("Gal", first.SuggestedName);

        var created = await (await client.PostJsonAsync("/api/auth/register", Profile(signupToken: first.SignupToken))).ReadAsync<AuthResultDto>();
        Assert.Equal(["contactVerified"], created.User!.Badges);

        var second = await (await factory.CreateApiClient().PostJsonAsync("/api/auth/external", new { provider = "google", idToken = factory.External.Issue(identity) })).ReadAsync<AuthResultDto>();
        Assert.Equal("signedIn", second.Status);
        Assert.Equal(created.User.Id, second.User!.Id);
    }

    [Fact]
    public async Task Apple_sign_in_links_to_an_existing_account_with_the_same_verified_email()
    {
        var (_, existing) = await factory.SignUpAsync("Ari");
        var identity = new ExternalIdentity(ExternalProvider.Apple, Guid.NewGuid().ToString(), existing.User!.Email, true, null);

        var res = await (await factory.CreateApiClient().PostJsonAsync("/api/auth/external", new { provider = "apple", idToken = factory.External.Issue(identity) })).ReadAsync<AuthResultDto>();

        Assert.Equal("signedIn", res.Status);
        Assert.Equal(existing.User.Id, res.User!.Id);
    }

    [Fact]
    public async Task External_sign_in_rejects_bad_tokens_and_unverified_emails()
    {
        var client = factory.CreateApiClient();
        var unverified = new ExternalIdentity(ExternalProvider.Google, Guid.NewGuid().ToString(), ApiHelpers.NewEmail("U"), false, null);

        var bad = await client.PostJsonAsync("/api/auth/external", new { provider = "google", idToken = "forged" });
        var notVerified = await client.PostJsonAsync("/api/auth/external", new { provider = "google", idToken = factory.External.Issue(unverified) });

        Assert.Equal("InvalidIdToken", await bad.ErrorCodeAsync());
        Assert.Equal("EmailNotVerified", await notVerified.ErrorCodeAsync());
    }

    [Fact]
    public async Task A_tampered_signup_token_is_refused()
    {
        var res = await factory.CreateApiClient().PostJsonAsync("/api/auth/register", Profile(signupToken: "eyJhbGciOiJIUzI1NiJ9.e30.forged"));

        Assert.Equal("SignupExpired", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task State_changing_requests_need_the_csrf_header()
    {
        var client = factory.CreateClient(); // no CSRF header

        var res = await client.PostJsonAsync("/api/auth/login", new { email = "a@example.com", password = "whatever123" });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal("CsrfHeaderMissing", await res.ErrorCodeAsync());
    }

    [Fact]
    public async Task Logout_ends_this_session_and_logout_all_ends_every_session()
    {
        var email = ApiHelpers.NewEmail("Multi");
        var phone = factory.CreateApiClient();
        await phone.PostJsonAsync("/api/auth/register", Profile(email, "correct horse battery"));
        var laptop = factory.CreateApiClient();
        await laptop.PostJsonAsync("/api/auth/login", new { email, password = "correct horse battery" });

        await laptop.PostAsync("/api/auth/logout", null);
        Assert.Null((await (await laptop.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);
        Assert.NotNull((await (await phone.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);

        await laptop.PostJsonAsync("/api/auth/login", new { email, password = "correct horse battery" });
        Assert.Equal(HttpStatusCode.NoContent, (await phone.PostAsync("/api/auth/logout-all", null)).StatusCode);
        Assert.Null((await (await laptop.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);
    }

    [Fact]
    public async Task A_ban_ends_the_session_and_blocks_sign_in()
    {
        var email = ApiHelpers.NewEmail("Banned");
        var client = factory.CreateApiClient();
        var auth = await (await client.PostJsonAsync("/api/auth/register", Profile(email, "correct horse battery"))).ReadAsync<AuthResultDto>();

        await using (var db = pg.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == auth.User!.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.BannedAt, DateTimeOffset.UtcNow));
        }

        Assert.Null((await (await client.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
        var login = await factory.CreateApiClient().PostJsonAsync("/api/auth/login", new { email, password = "correct horse battery" });
        Assert.Equal("AccountSuspended", await login.ErrorCodeAsync());
    }

    [Fact]
    public async Task Password_reset_by_email_code_signs_out_other_sessions()
    {
        var email = ApiHelpers.NewEmail("Reset");
        var old = factory.CreateApiClient();
        await old.PostJsonAsync("/api/auth/register", Profile(email, "correct horse battery"));
        var client = factory.CreateApiClient();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostJsonAsync("/api/auth/password/forgot", new { email })).StatusCode);
        var reset = await client.PostJsonAsync("/api/auth/password/reset", new { email, code = factory.Emails.LatestCode(email), newPassword = "a brand new secret" });

        Assert.Equal("signedIn", (await reset.ReadAsync<AuthResultDto>()).Status);
        Assert.Null((await (await old.GetAsync("/api/auth/me")).ReadAsync<SessionDto>()).User);
        Assert.Equal("InvalidCredentials", await (await client.PostJsonAsync("/api/auth/login", new { email, password = "correct horse battery" })).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.PostJsonAsync("/api/auth/login", new { email, password = "a brand new secret" })).StatusCode);
    }

    [Fact]
    public async Task Onboarding_step_three_saves_interests_and_languages()
    {
        var (client, _) = await factory.SignUpAsync("Onb");

        var me = await (await client.PatchJsonAsync("/api/me", new { interests = new[] { "hiking", "food" }, languages = new[] { "HE", "en" } })).ReadAsync<MeDto>();
        var bad = await client.PatchJsonAsync("/api/me", new { interests = new[] { "<script>" } });

        Assert.Equal(["hiking", "food"], me.Interests);
        Assert.Equal(["he", "en"], me.Languages);
        Assert.Equal("InvalidInterest", await bad.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateApiClient().PatchJsonAsync("/api/me", new { bio = "x" })).StatusCode);
    }
}
