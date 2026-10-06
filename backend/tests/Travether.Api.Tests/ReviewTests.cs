using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Travether.Api.Cards;
using Travether.Api.Controllers;
using Travether.Api.Domain;
using Travether.Api.Notifications;
using Travether.Api.Plans;
using Travether.Api.Reviews;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class ReviewTests(PostgresFixture pg) : IAsyncLifetime
{
    private ApiFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory(pg.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await factory.DisposeAsync();

    private sealed record Pair(HttpClient Host, Guid HostId, HttpClient Guest, Guid GuestId, Guid PlanId);

    /// <summary>A host and a card member who joined the host's plan, which started <paramref name="hoursAgo"/> hours ago.</summary>
    private async Task<Pair> FinishedPlanAsync(double hoursAgo = 3)
    {
        var (host, hostAuth) = await factory.SignUpAsync("Host");
        var card = await (await host.PostJsonAsync("/api/cards", CardTests.NewCard(startInDays: 5, days: 10))).ReadAsync<CardDto>();
        var plan = await (await host.PostJsonAsync($"/api/cards/{card.Id}/plans", PlanTests.NewPlan())).ReadAsync<PlanDto>();
        var (guest, guestAuth) = await factory.SignUpAsync("Guest");
        await using (var db = pg.CreateDbContext())
        {
            db.CardMembers.Add(new CardMember { CardId = card.Id, UserId = guestAuth.User!.Id, Role = CardRole.Member, Status = MembershipStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsync($"/api/plans/{plan.Id}/join", null)).StatusCode);
        await MoveStartAsync(plan.Id, DateTimeOffset.UtcNow.AddHours(-hoursAgo));
        return new Pair(host, hostAuth.User!.Id, guest, guestAuth.User.Id, plan.Id);
    }

    private async Task MoveStartAsync(Guid planId, DateTimeOffset startsAt)
    {
        await using var db = pg.CreateDbContext();
        await db.ActivityPlans.Where(p => p.Id == planId).ExecuteUpdateAsync(u => u.SetProperty(p => p.StartsAt, startsAt).SetProperty(p => p.CreatedAt, startsAt.AddDays(-3)));
    }

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid planId, string answer) =>
        client.PostJsonAsync($"/api/plans/{planId}/meet", new { answer });

    private static Task<HttpResponseMessage> ReviewAsync(HttpClient client, Guid planId, Guid revieweeId, int stars = 5, string? text = "Great company") =>
        client.PostJsonAsync($"/api/plans/{planId}/reviews", new { revieweeId, stars, text });

    private static async Task<WrapUpDto> WrapUpAsync(HttpClient client, Guid planId) =>
        await (await client.GetAsync($"/api/plans/{planId}/wrap-up")).ReadAsync<WrapUpDto>();

    private static async Task<ReviewPageDto> AboutAsync(HttpClient client, Guid userId) =>
        await (await client.GetAsync($"/api/users/{userId}/reviews")).ReadAsync<ReviewPageDto>();

    [Fact]
    public async Task Reviews_stay_hidden_until_both_sides_review()
    {
        var p = await FinishedPlanAsync();
        var visitor = factory.CreateApiClient();

        await AnswerAsync(p.Host, p.PlanId, "met");
        var waiting = await WrapUpAsync(p.Host, p.PlanId);
        Assert.Equal(MeetAnswer.Met, waiting.MyAnswer);
        Assert.Equal(ReviewState.Waiting, Assert.Single(waiting.People).State);
        await AnswerAsync(p.Guest, p.PlanId, "met");

        var open = Assert.Single((await WrapUpAsync(p.Host, p.PlanId)).People);
        Assert.Equal(ReviewState.Open, open.State);
        Assert.NotNull(open.ReviewUntil);

        var afterFirst = await (await ReviewAsync(p.Host, p.PlanId, p.GuestId, 5, "Easy to hike with")).ReadAsync<WrapUpDto>();
        Assert.Equal(ReviewState.Reviewed, afterFirst.People[0].State);
        Assert.Equal("Easy to hike with", afterFirst.People[0].MyReview!.Text);

        // The guest knows a review exists but can't read it, and neither can anyone else.
        var guestSide = Assert.Single((await WrapUpAsync(p.Guest, p.PlanId)).People);
        Assert.True(guestSide.TheyReviewedMe);
        Assert.Null(guestSide.TheirReview);
        Assert.Empty((await AboutAsync(visitor, p.GuestId)).Items);
        Assert.Contains((await (await p.Guest.GetAsync("/api/notifications")).ReadAsync<NotificationPageDto>()).Items, n => n.Type == NotificationTypes.ReviewReceived);

        await ReviewAsync(p.Guest, p.PlanId, p.HostId, 4, "On time, good pace");

        var published = Assert.Single((await AboutAsync(visitor, p.GuestId)).Items);
        Assert.Equal(5, published.Stars);
        Assert.Equal("Host", published.Reviewer!.DisplayName);
        Assert.Equal("Sunrise hike to Doi Suthep", published.PlanTitle);
        Assert.Equal("On time, good pace", (await WrapUpAsync(p.Host, p.PlanId)).People[0].TheirReview!.Text);
        Assert.Equal("AlreadyReviewed", await (await ReviewAsync(p.Host, p.PlanId, p.GuestId)).ErrorCodeAsync());

        // One public reply, by the person reviewed.
        Assert.Equal(HttpStatusCode.NotFound, (await p.Host.PostJsonAsync($"/api/reviews/{published.Id}/reply", new { text = "Me?" })).StatusCode);
        var replied = await (await p.Guest.PostJsonAsync($"/api/reviews/{published.Id}/reply", new { text = "Thanks, see you on the next one!" })).ReadAsync<ReviewDto>();
        Assert.Equal("Thanks, see you on the next one!", replied.Reply);
        Assert.Equal("AlreadyReplied", await (await p.Guest.PostJsonAsync($"/api/reviews/{published.Id}/reply", new { text = "Again" })).ErrorCodeAsync());
    }

    [Fact]
    public async Task Answers_open_at_the_start_once_per_person_for_participants_only()
    {
        var p = await FinishedPlanAsync();
        var (stranger, _) = await factory.SignUpAsync("Stranger");

        Assert.Equal("NotParticipant", await (await AnswerAsync(stranger, p.PlanId, "met")).ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, (await AnswerAsync(p.Guest, p.PlanId, "met")).StatusCode);
        Assert.Equal("AlreadyAnswered", await (await AnswerAsync(p.Guest, p.PlanId, "noShow")).ErrorCodeAsync());

        await MoveStartAsync(p.PlanId, DateTimeOffset.UtcNow.AddHours(5));
        Assert.Equal("PlanNotOver", await (await AnswerAsync(p.Host, p.PlanId, "met")).ErrorCodeAsync());

        await MoveStartAsync(p.PlanId, DateTimeOffset.UtcNow.AddDays(-15));
        Assert.Equal("AnswerClosed", await (await AnswerAsync(p.Host, p.PlanId, "met")).ErrorCodeAsync());
    }

    [Fact]
    public async Task A_no_show_answer_never_shows_and_reviewing_stays_closed()
    {
        var p = await FinishedPlanAsync();

        await AnswerAsync(p.Host, p.PlanId, "met");
        await AnswerAsync(p.Guest, p.PlanId, "noShow");

        Assert.Equal(ReviewState.Waiting, (await WrapUpAsync(p.Host, p.PlanId)).People[0].State);
        Assert.Equal(ReviewState.Closed, (await WrapUpAsync(p.Guest, p.PlanId)).People[0].State);
        Assert.Equal("ReviewNotOpen", await (await ReviewAsync(p.Host, p.PlanId, p.GuestId)).ErrorCodeAsync());
        Assert.Equal("InvalidStars", await (await ReviewAsync(p.Host, p.PlanId, p.GuestId, stars: 6)).ErrorCodeAsync());
    }

    [Fact]
    public async Task A_single_review_goes_public_when_the_window_closes()
    {
        var p = await FinishedPlanAsync();
        await AnswerAsync(p.Host, p.PlanId, "met");
        await AnswerAsync(p.Guest, p.PlanId, "met");
        await ReviewAsync(p.Host, p.PlanId, p.GuestId, 3, "Fine");
        Assert.Empty((await AboutAsync(p.Guest, p.GuestId)).Items);

        await using (var db = pg.CreateDbContext())
        {
            await db.MeetConfirmations.Where(m => m.PlanId == p.PlanId).ExecuteUpdateAsync(u => u.SetProperty(m => m.AnsweredAt, DateTimeOffset.UtcNow.AddDays(-15)));
        }

        Assert.Equal("Fine", Assert.Single((await AboutAsync(p.Guest, p.GuestId)).Items).Text);
        Assert.Equal(ReviewState.Closed, (await WrapUpAsync(p.Guest, p.PlanId)).People[0].State);

        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReviewService>().PublishDueAsync(default);
        await using var check = pg.CreateDbContext();
        Assert.NotNull((await check.Reviews.SingleAsync(r => r.PlanId == p.PlanId)).PublishedAt);
    }

    [Fact]
    public async Task The_day_after_everyone_is_asked_once_and_the_plan_is_done()
    {
        var p = await FinishedPlanAsync(hoursAgo: 40);

        using (var scope = factory.Services.CreateScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<NotificationJobs>();
            await jobs.MeetPromptsAsync(default);
            await jobs.MeetPromptsAsync(default);
        }

        foreach (var client in new[] { p.Host, p.Guest })
        {
            var prompts = (await (await client.GetAsync("/api/notifications")).ReadAsync<NotificationPageDto>()).Items.Where(n => n.Type == NotificationTypes.MeetPrompt);
            Assert.Equal($"/plans/{p.PlanId}/review", Assert.Single(prompts).Payload.Url);
        }

        var pending = Assert.Single(await (await p.Guest.GetAsync("/api/me/wrap-ups")).ReadAsync<List<PendingWrapUpDto>>(), w => w.PlanId == p.PlanId);
        Assert.True(pending.NeedsAnswer);
        await using var db = pg.CreateDbContext();
        Assert.Equal(PlanStatus.Done, (await db.ActivityPlans.SingleAsync(x => x.Id == p.PlanId)).Status);
    }

    [Theory]
    [InlineData(null, null, false, 1, ReviewState.Waiting)]
    [InlineData(MeetAnswer.Met, MeetAnswer.NoShow, false, 1, ReviewState.Waiting)]
    [InlineData(MeetAnswer.Met, null, false, 20, ReviewState.Closed)]
    [InlineData(MeetAnswer.Cancelled, MeetAnswer.Met, false, 1, ReviewState.Closed)]
    [InlineData(MeetAnswer.Met, MeetAnswer.Met, false, 1, ReviewState.Open)]
    [InlineData(MeetAnswer.Met, MeetAnswer.Met, true, 1, ReviewState.Reviewed)]
    public void Review_state_follows_both_answers(MeetAnswer? mine, MeetAnswer? theirs, bool reviewed, int daysAfterStart, ReviewState expected)
    {
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        MeetConfirmation? Answer(MeetAnswer? a) => a is { } x ? new MeetConfirmation { Answer = x, AnsweredAt = start.AddHours(30) } : null;

        Assert.Equal(expected, ReviewRules.StateFor(Answer(mine), Answer(theirs), reviewed, ReviewRules.AnswerUntil(start), start.AddDays(daysAfterStart)));
    }
}
