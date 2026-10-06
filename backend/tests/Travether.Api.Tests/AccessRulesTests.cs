using Travether.Api.Authorization;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

public sealed class AccessRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("2008-10-06", true)] // 18 today
    [InlineData("2008-10-07", false)] // 18 tomorrow
    [InlineData("1990-02-28", true)]
    public void Adult_means_18_or_older(string dob, bool adult) =>
        Assert.Equal(adult, AccessRules.IsAdult(DateOnly.Parse(dob, System.Globalization.CultureInfo.InvariantCulture), new DateOnly(2026, 10, 6)));

    [Theory]
    [InlineData(CardVisibility.Public, false, CardAccess.Preview)]
    [InlineData(CardVisibility.InviteOnly, false, CardAccess.None)]
    [InlineData(CardVisibility.InviteOnly, true, CardAccess.Preview)]
    public void Outsiders_see_a_preview_of_public_cards_or_via_the_share_link(CardVisibility visibility, bool viaLink, CardAccess expected) =>
        Assert.Equal(expected, AccessRules.CardAccessFor(visibility, activeRole: null, viaLink, blockedByOwner: false));

    [Fact]
    public void Someone_blocked_by_the_owner_cannot_see_the_card_even_with_the_link() =>
        Assert.Equal(CardAccess.None, AccessRules.CardAccessFor(CardVisibility.Public, null, viaShareLink: true, blockedByOwner: true));

    [Theory]
    [InlineData(CardRole.Member, true, false, false)]
    [InlineData(CardRole.CoAdmin, true, true, false)]
    [InlineData(CardRole.Owner, true, true, true)]
    public void Card_permissions_by_role(CardRole role, bool inside, bool decide, bool manage)
    {
        var access = AccessRules.CardAccessFor(CardVisibility.Public, role, false, false);
        Assert.Equal(inside, AccessRules.CanSeeCardInside(access));
        Assert.Equal(decide, AccessRules.CanDecideCardRequests(access));
        Assert.Equal(manage, AccessRules.CanManageCard(access));
        Assert.False(AccessRules.CanRequestToJoinCard(access));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    public void Only_participants_and_the_host_see_the_exact_location(bool host, bool participant, bool exact)
    {
        var access = AccessRules.PlanAccessFor(host, participant, CardAccess.None, blockedByHost: false);
        Assert.Equal(exact, AccessRules.CanSeeExactLocation(access));
        Assert.Equal(exact, AccessRules.CanSeePlanChat(access));
    }

    [Fact]
    public void Members_of_the_plans_card_who_have_not_joined_see_it_like_the_public()
    {
        var access = AccessRules.PlanAccessFor(false, false, CardAccess.Member, blockedByHost: false);
        Assert.Equal(PlanAccess.Public, access);
        Assert.False(AccessRules.CanSeeExactLocation(access));
    }

    [Fact]
    public void Co_admins_of_the_plans_card_can_decide_requests_but_members_cannot()
    {
        Assert.True(AccessRules.CanDecidePlanRequests(PlanAccess.Public, CardAccess.CoAdmin));
        Assert.False(AccessRules.CanDecidePlanRequests(PlanAccess.Participant, CardAccess.Member));
        Assert.True(AccessRules.CanDecidePlanRequests(PlanAccess.Host, CardAccess.Member));
    }

    [Theory]
    [InlineData(400, 1, true)]
    [InlineData(1400, 1, false)]
    [InlineData(2600, 3, false)]
    [InlineData(10_000, 10, false)]
    [InlineData(12_400, 10, false)]
    [InlineData(13_000, 15, false)]
    public void Public_distances_are_rounded(double meters, int km, bool under)
    {
        Assert.Equal(new ApproxDistance(km, under), AccessRules.RoundDistance(meters));
    }

    private static ActivityPlan Plan(PlanAudience audience = PlanAudience.Open, PlanStatus status = PlanStatus.Open) => new()
    {
        Title = "Sunrise hike",
        Origin = new NetTopologySuite.Geometries.Point(98.98, 18.79) { SRID = 4326 },
        OriginAreaLabel = "Old City",
        Destination = "Doi Suthep",
        TimeZoneId = "Asia/Bangkok",
        SeatLimit = 4,
        Audience = audience,
        Status = status,
    };

    [Fact]
    public void Join_request_checks()
    {
        var open = Plan();
        Assert.Equal(JoinDenial.None, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Public, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.Blocked, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.None, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.AlreadyParticipant, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Participant, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.OwnCardMembersSelfJoin, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Public, CardAccess.Member, false, null, true));
        Assert.Equal(JoinDenial.Full, AccessRules.CheckPlanJoinRequest(open, 4, PlanAccess.Public, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.NotOpen, AccessRules.CheckPlanJoinRequest(Plan(status: PlanStatus.Cancelled), 1, PlanAccess.Public, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.AlreadyRequested, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Public, CardAccess.None, true, null, true));
        Assert.Equal(JoinDenial.SourceCardNotYours, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Public, CardAccess.None, false, CardAccess.Preview, true));
        Assert.Equal(JoinDenial.PartyNotInSourceCard, AccessRules.CheckPlanJoinRequest(open, 2, PlanAccess.Public, CardAccess.None, false, CardAccess.Member, false));
    }

    [Fact]
    public void Groups_only_plans_need_a_source_card()
    {
        var plan = Plan(PlanAudience.GroupsOnly);
        Assert.Equal(JoinDenial.GroupsOnlyNeedsCard, AccessRules.CheckPlanJoinRequest(plan, 1, PlanAccess.Public, CardAccess.None, false, null, true));
        Assert.Equal(JoinDenial.None, AccessRules.CheckPlanJoinRequest(plan, 1, PlanAccess.Public, CardAccess.None, false, CardAccess.Member, true));
    }

    private static MeetConfirmation Met(DateTimeOffset at) => new() { Answer = MeetAnswer.Met, AnsweredAt = at };

    [Fact]
    public void Review_window_opens_only_when_both_met_and_lasts_14_days_after_the_second_answer()
    {
        var mine = Met(Now);
        var theirs = Met(Now.AddDays(2));

        Assert.Equal(Now.AddDays(16), AccessRules.ReviewWindowEnd(mine, theirs));
        Assert.True(AccessRules.CanReview(mine, theirs, alreadyReviewed: false, Now.AddDays(16)));
        Assert.False(AccessRules.CanReview(mine, theirs, alreadyReviewed: false, Now.AddDays(16).AddSeconds(1)));
        Assert.False(AccessRules.CanReview(mine, theirs, alreadyReviewed: true, Now.AddDays(3)));
        Assert.Null(AccessRules.ReviewWindowEnd(mine, new MeetConfirmation { Answer = MeetAnswer.NoShow, AnsweredAt = Now }));
        Assert.Null(AccessRules.ReviewWindowEnd(mine, null));
    }

    [Fact]
    public void Reviews_stay_hidden_until_both_submit_or_the_window_closes()
    {
        var reviewer = Guid.NewGuid();
        var review = new Review { ReviewerId = reviewer, RevieweeId = Guid.NewGuid(), Stars = 5 };
        var end = Now.AddDays(14);

        var hidden = AccessRules.IsReviewPublished(counterpartSubmitted: false, end, Now);
        Assert.False(hidden);
        Assert.True(AccessRules.CanSeeReview(reviewer, review, hidden)); // the author always sees their own
        Assert.False(AccessRules.CanSeeReview(review.RevieweeId, review, hidden));

        Assert.True(AccessRules.IsReviewPublished(counterpartSubmitted: true, end, Now));
        Assert.True(AccessRules.IsReviewPublished(counterpartSubmitted: false, end, end.AddTicks(1)));
    }

    [Theory]
    [InlineData(2, 4.0, null)]
    [InlineData(3, 4.666, 4.7)]
    public void Average_rating_needs_three_reviews(int count, double avg, double? expected) =>
        Assert.Equal(expected, AccessRules.PublicAverage(count, avg));

    [Fact]
    public void Profile_access_levels()
    {
        var me = Guid.NewGuid();
        var other = Guid.NewGuid();
        Assert.Equal(ProfileAccess.Self, AccessRules.ProfileAccessFor(me, UserRole.Traveler, me, false, false));
        Assert.Equal(ProfileAccess.Public, AccessRules.ProfileAccessFor(me, UserRole.Traveler, other, false, false));
        Assert.Equal(ProfileAccess.CoParticipant, AccessRules.ProfileAccessFor(me, UserRole.Traveler, other, false, true));
        Assert.Equal(ProfileAccess.None, AccessRules.ProfileAccessFor(me, UserRole.Traveler, other, true, true));
        Assert.Equal(ProfileAccess.Moderator, AccessRules.ProfileAccessFor(me, UserRole.Moderator, other, true, false));
    }
}
