namespace Travether.Api.Domain;

/// <summary>A trip group: destination, regions and dates (PLAN.md §4.2).</summary>
public sealed class VacationCard
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; set; }

    /// <summary>One or more city/region labels picked from the places provider.</summary>
    public List<string> Regions { get; set; } = [];

    public DateOnly StartsOn { get; set; }

    /// <summary>Inclusive. Must be on or after <see cref="StartsOn"/>.</summary>
    public DateOnly EndsOn { get; set; }

    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public CardVisibility Visibility { get; set; }

    /// <summary>Unguessable slug for the share link and QR code.</summary>
    public required string ShareSlug { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public List<CardMember> Members { get; set; } = [];
    public List<ActivityPlan> Plans { get; set; } = [];
}

public sealed class CardMember
{
    public Guid CardId { get; set; }
    public VacationCard Card { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public CardRole Role { get; set; }
    public MembershipStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class CardRequest
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public VacationCard Card { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string? Message { get; set; }
    public RequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
