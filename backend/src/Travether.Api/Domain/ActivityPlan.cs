using NetTopologySuite.Geometries;

namespace Travether.Api.Domain;

/// <summary>What, where and when, published from a Vacation Card (PLAN.md §4.3).</summary>
public sealed class ActivityPlan
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public VacationCard Card { get; set; } = null!;
    public Guid HostId { get; set; }
    public User Host { get; set; } = null!;

    public required string Title { get; set; }
    public PlanCategory Category { get; set; }

    /// <summary>
    /// Exact meeting point (WGS 84, geography). Private: only approved participants see it.
    /// Everyone else gets <see cref="OriginAreaLabel"/> and a rounded distance.
    /// </summary>
    public required Point Origin { get; set; }

    /// <summary>
    /// <see cref="Origin"/> snapped to a ~1 km grid (stored generated column). Public distances and
    /// discovery radius queries use this point, so rounded distances can't be trilaterated back to
    /// the exact meeting point.
    /// </summary>
    public Point OriginPublic { get; private set; } = null!;

    /// <summary>Name of the exact meeting point, e.g. "Tha Phae Gate". Private like <see cref="Origin"/>.</summary>
    public string OriginName { get; set; } = "";

    /// <summary>Public, coarse label for the meeting area, e.g. "Old City, Chiang Mai".</summary>
    public required string OriginAreaLabel { get; set; }

    public required string Destination { get; set; }

    /// <summary>When Exact, <see cref="Destination"/> is private like <see cref="Origin"/>.</summary>
    public LocationPrecision DestinationPrecision { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>IANA zone of the meeting point, for local dates, reminders and the "Did you meet?" prompt.</summary>
    public required string TimeZoneId { get; set; }

    public string? Purpose { get; set; }

    /// <summary>Total seats including the host. At least 2.</summary>
    public int SeatLimit { get; set; }

    public PlanAudience Audience { get; set; }
    public PlanStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public List<PlanParticipant> Participants { get; set; } = [];
}

public sealed class PlanParticipant
{
    public Guid PlanId { get; set; }
    public ActivityPlan Plan { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>The card the participant came from (the host's card, or the requester's own card).</summary>
    public Guid? SourceCardId { get; set; }

    public MembershipStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class PlanRequest
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public ActivityPlan Plan { get; set; } = null!;
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;

    /// <summary>Required for "Groups only" plans: the requester's own card.</summary>
    public Guid? SourceCardId { get; set; }

    /// <summary>Members of the requester's card joining with them (requester excluded).</summary>
    public List<Guid> PartyUserIds { get; set; } = [];

    public string? Message { get; set; }
    public RequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
