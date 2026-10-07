using Travether.Api.Domain;

namespace Travether.Api.Authorization;

/// <summary>How a viewer relates to a Vacation Card. Ordered: each level includes the ones before it.</summary>
public enum CardAccess
{
    /// <summary>Can't see the card at all (invite-only without the link, blocked, or signed-out on a private card).</summary>
    None,

    /// <summary>Public preview: name, destination, dates, cover, member count.</summary>
    Preview,

    Member,
    CoAdmin,
    Owner,
}

/// <summary>How a viewer relates to an Activity Plan.</summary>
public enum PlanAccess
{
    None,

    /// <summary>Area label, rounded distance, date/time, seats left, host's public profile.</summary>
    Public,

    /// <summary>Approved participant: exact meeting point, exact destination, plan chat.</summary>
    Participant,

    Host,
}

/// <summary>Which profile fields a viewer may see (PLAN.md §4.1).</summary>
public enum ProfileAccess
{
    None,

    /// <summary>Display name, age, country, photo, bio, languages, interests, badges, rating.</summary>
    Public,

    /// <summary>Shares an active card or plan with the user: adds full name.</summary>
    CoParticipant,

    /// <summary>Moderators: adds email for abuse handling. Never phone or date of birth.</summary>
    Moderator,

    /// <summary>The user themself: everything.</summary>
    Self,
}

/// <summary>Why a join request can't be made. Returned to the client as an error code.</summary>
public enum JoinDenial
{
    None,
    Blocked,
    NotOpen,
    Full,
    AlreadyParticipant,
    AlreadyRequested,
    OwnCardMembersSelfJoin,
    GroupsOnlyNeedsCard,
    SourceCardNotYours,
    PartyNotInSourceCard,
}

public readonly record struct ApproxDistance(int Km, bool UnderOneKm);

/// <summary>
/// Pure authorization rules. Every endpoint decides access with these functions plus the facts
/// <see cref="AccessQueries"/> loads; the client never decides. See docs/AUTHORIZATION.md.
/// </summary>
public static class AccessRules
{
    public const int MinimumAge = 18;
    public const int ReviewWindowDays = 14;
    public const int MinReviewsForAverage = 3;

    /// <summary>Grid for the public meeting point (0.01° ≈ 1.1 km north–south).</summary>
    public const double PublicGridDegrees = 0.01;

    // ---------- People ----------

    public static int AgeOn(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public static bool IsAdult(DateOnly dateOfBirth, DateOnly today) => AgeOn(dateOfBirth, today) >= MinimumAge;

    /// <summary>A banned or deleted account behaves as if it doesn't exist, as viewer and as target.</summary>
    public static bool IsActive(User user) => user.BannedAt is null && user.DeletedAt is null;

    public static ProfileAccess ProfileAccessFor(Guid? viewerId, UserRole? viewerRole, Guid targetId, bool blockedEitherWay, bool coParticipants)
    {
        if (viewerId == targetId)
        {
            return ProfileAccess.Self;
        }

        if (viewerRole == UserRole.Moderator)
        {
            return ProfileAccess.Moderator;
        }

        if (blockedEitherWay)
        {
            return ProfileAccess.None;
        }

        return coParticipants ? ProfileAccess.CoParticipant : ProfileAccess.Public;
    }

    // ---------- Vacation Cards ----------

    public static CardAccess CardAccessFor(CardVisibility visibility, CardRole? activeRole, bool viaShareLink, bool blockedByOwner)
    {
        if (activeRole is { } role)
        {
            return role switch
            {
                CardRole.Owner => CardAccess.Owner,
                CardRole.CoAdmin => CardAccess.CoAdmin,
                _ => CardAccess.Member,
            };
        }

        if (blockedByOwner)
        {
            return CardAccess.None;
        }

        return visibility == CardVisibility.Public || viaShareLink ? CardAccess.Preview : CardAccess.None;
    }

    /// <summary>Member list, plans list, card chat.</summary>
    public static bool CanSeeCardInside(CardAccess access) => access >= CardAccess.Member;

    public static bool CanDecideCardRequests(CardAccess access) => access >= CardAccess.CoAdmin;

    /// <summary>Appoint co-admins, remove members, edit and delete the card.</summary>
    public static bool CanManageCard(CardAccess access) => access == CardAccess.Owner;

    public static bool CanRequestToJoinCard(CardAccess access) => access == CardAccess.Preview;

    // ---------- Activity Plans ----------

    public static PlanAccess PlanAccessFor(bool isHost, bool isActiveParticipant, CardAccess planCardAccess, bool blockedByHost)
    {
        if (isHost)
        {
            return PlanAccess.Host;
        }

        if (isActiveParticipant)
        {
            return PlanAccess.Participant;
        }

        // A block with the host hides the plan, even from members of the host's own card.
        if (blockedByHost)
        {
            return PlanAccess.None;
        }

        // Members of the plan's own card see it like the public until they join.
        return PlanAccess.Public;
    }

    /// <summary>Exact meeting point and exact destination.</summary>
    public static bool CanSeeExactLocation(PlanAccess access) => access >= PlanAccess.Participant;

    public static bool CanSeePlanChat(PlanAccess access) => access >= PlanAccess.Participant;

    /// <summary>The host and the co-admins/owner of the plan's card act for the group.</summary>
    public static bool CanDecidePlanRequests(PlanAccess access, CardAccess planCardAccess) =>
        access == PlanAccess.Host || planCardAccess >= CardAccess.CoAdmin;

    public static JoinDenial CheckPlanJoinRequest(
        ActivityPlan plan,
        int activeParticipantCount,
        PlanAccess requesterAccess,
        CardAccess requesterAccessToPlanCard,
        bool hasOpenRequest,
        CardAccess? sourceCardAccess,
        bool partyAllInSourceCard)
    {
        if (requesterAccess == PlanAccess.None)
        {
            return JoinDenial.Blocked;
        }

        if (requesterAccess >= PlanAccess.Participant)
        {
            return JoinDenial.AlreadyParticipant;
        }

        if (requesterAccessToPlanCard >= CardAccess.Member)
        {
            return JoinDenial.OwnCardMembersSelfJoin;
        }

        if (plan.Status != PlanStatus.Open)
        {
            return JoinDenial.NotOpen;
        }

        if (activeParticipantCount >= plan.SeatLimit)
        {
            return JoinDenial.Full;
        }

        if (hasOpenRequest)
        {
            return JoinDenial.AlreadyRequested;
        }

        if (sourceCardAccess is null)
        {
            return plan.Audience == PlanAudience.GroupsOnly ? JoinDenial.GroupsOnlyNeedsCard : JoinDenial.None;
        }

        if (sourceCardAccess < CardAccess.Member)
        {
            return JoinDenial.SourceCardNotYours;
        }

        return partyAllInSourceCard ? JoinDenial.None : JoinDenial.PartyNotInSourceCard;
    }

    /// <summary>Public distance: under 1 km, whole km up to 10, then steps of 5 km.</summary>
    public static ApproxDistance RoundDistance(double meters)
    {
        if (meters < 1000)
        {
            return new ApproxDistance(1, UnderOneKm: true);
        }

        var km = meters / 1000;
        var rounded = km <= 10 ? Math.Round(km, MidpointRounding.AwayFromZero) : Math.Round(km / 5, MidpointRounding.AwayFromZero) * 5;
        return new ApproxDistance((int)rounded, UnderOneKm: false);
    }

    // ---------- Chat ----------

    public static bool CanReadConversation(ConversationType type, CardAccess cardAccess, PlanAccess planAccess, bool isDirectMember, bool directBlocked) =>
        type switch
        {
            ConversationType.Card => CanSeeCardInside(cardAccess),
            ConversationType.Plan => CanSeePlanChat(planAccess),
            ConversationType.Direct => isDirectMember && !directBlocked,
            _ => false,
        };

    // ---------- Ratings (PLAN.md §4.6) ----------

    /// <summary>
    /// Reviewing opens only when both people answered "Yes, we met", and stays open for
    /// <see cref="ReviewWindowDays"/> days after the second answer.
    /// </summary>
    public static DateTimeOffset? ReviewWindowEnd(MeetConfirmation? mine, MeetConfirmation? theirs)
    {
        if (mine?.Answer != MeetAnswer.Met || theirs?.Answer != MeetAnswer.Met)
        {
            return null;
        }

        var later = mine.AnsweredAt > theirs.AnsweredAt ? mine.AnsweredAt : theirs.AnsweredAt;
        return later.AddDays(ReviewWindowDays);
    }

    public static bool CanReview(MeetConfirmation? mine, MeetConfirmation? theirs, bool alreadyReviewed, DateTimeOffset now) =>
        !alreadyReviewed && ReviewWindowEnd(mine, theirs) is { } end && now <= end;

    /// <summary>
    /// Double-blind: a review is visible to anyone but its author only once the counterpart review
    /// exists or the window has closed. Computed from facts, so no background job is needed for correctness.
    /// </summary>
    public static bool IsReviewPublished(bool counterpartSubmitted, DateTimeOffset? windowEnd, DateTimeOffset now) =>
        counterpartSubmitted || (windowEnd is { } end && now > end);

    public static bool CanSeeReview(Guid? viewerId, Review review, bool published) =>
        viewerId == review.ReviewerId || published;

    /// <summary>The average is shown only from <see cref="MinReviewsForAverage"/> reviews; the count is always shown.</summary>
    public static double? PublicAverage(int publishedCount, double average) =>
        publishedCount >= MinReviewsForAverage ? Math.Round(average, 1) : null;
}
