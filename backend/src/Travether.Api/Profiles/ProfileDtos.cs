using Travether.Api.Authorization;
using Travether.Api.Domain;

namespace Travether.Api.Profiles;

/// <summary>Everything about the signed-in user, for their own eyes only (ProfileAccess.Self).</summary>
public sealed record MeDto(
    Guid Id,
    string DisplayName,
    string FullName,
    string Email,
    string? Phone,
    DateOnly DateOfBirth,
    int Age,
    string CountryCode,
    string? PhotoUrl,
    string? Bio,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Interests,
    IReadOnlyList<string> Badges,
    UserRole Role,
    bool HasPassword,
    DateTimeOffset CreatedAt,
    ProfileStrength Strength)
{
    public static MeDto From(User u, DateOnly today) => new(
        u.Id, u.DisplayName, u.FullName, u.Email, u.Phone, u.DateOfBirth, AccessRules.AgeOn(u.DateOfBirth, today),
        u.CountryCode, u.PhotoUrl, u.Bio, u.Languages, u.Interests, BadgeNames(u.VerificationBadges), u.Role,
        u.PasswordHash is not null, u.CreatedAt, ProfileStrength.Of(u));

    /// <summary>Badges as stable names for the client: contactVerified, photoVerified, idVerified.</summary>
    public static IReadOnlyList<string> BadgeNames(VerificationBadges badges) =>
        Enum.GetValues<VerificationBadges>()
            .Where(b => b != VerificationBadges.None && badges.HasFlag(b))
            .Select(b => char.ToLowerInvariant(b.ToString()[0]) + b.ToString()[1..])
            .ToList();
}

/// <summary>
/// How complete a profile is (PLAN.md §4.1: start minimal, fill in gradually). <c>Missing</c> lists the
/// next things to add, most valuable first, so the app can prompt for one at a time.
/// </summary>
public sealed record ProfileStrength(int Percent, IReadOnlyList<string> Missing)
{
    private static readonly (string Key, int Weight, Func<User, bool> Done)[] Items =
    [
        ("contactVerified", 30, u => u.VerificationBadges.HasFlag(VerificationBadges.ContactVerified)),
        ("photo", 25, u => u.PhotoUrl is not null),
        ("bio", 15, u => !string.IsNullOrWhiteSpace(u.Bio)),
        ("interests", 15, u => u.Interests.Count > 0),
        ("languages", 15, u => u.Languages.Count > 0),
    ];

    public static ProfileStrength Of(User u) => new(
        Items.Where(i => i.Done(u)).Sum(i => i.Weight),
        Items.Where(i => !i.Done(u)).Select(i => i.Key).ToList());
}

public sealed record RatingSummary(double? Average, int Count);

/// <summary>
/// Another traveler's profile, cut to what the viewer may see (docs/AUTHORIZATION.md, People):
/// full name only for co-participants, email only for moderators, never phone or date of birth.
/// </summary>
public sealed record PublicProfileDto(
    Guid Id,
    string DisplayName,
    int Age,
    string CountryCode,
    string? PhotoUrl,
    string? Bio,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Interests,
    IReadOnlyList<string> Badges,
    RatingSummary Rating,
    DateTimeOffset MemberSince,
    string? FullName,
    string? Email)
{
    public static PublicProfileDto From(User u, ProfileAccess access, RatingSummary rating, DateOnly today) => new(
        u.Id, u.DisplayName, AccessRules.AgeOn(u.DateOfBirth, today), u.CountryCode, u.PhotoUrl, u.Bio, u.Languages,
        u.Interests, MeDto.BadgeNames(u.VerificationBadges), rating, u.CreatedAt,
        access >= ProfileAccess.CoParticipant ? u.FullName : null,
        access >= ProfileAccess.Moderator ? u.Email : null);
}

/// <summary>Who a person is in lists (members, participants, requesters): public fields only.</summary>
public sealed record PersonDto(Guid Id, string DisplayName, int Age, string CountryCode, string? PhotoUrl, IReadOnlyList<string> Badges)
{
    public static PersonDto From(User u, DateOnly today) =>
        new(u.Id, u.DisplayName, AccessRules.AgeOn(u.DateOfBirth, today), u.CountryCode, u.PhotoUrl, MeDto.BadgeNames(u.VerificationBadges));
}
