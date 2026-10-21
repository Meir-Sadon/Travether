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
    DateTimeOffset CreatedAt)
{
    public static MeDto From(User u, DateOnly today) => new(
        u.Id, u.DisplayName, u.FullName, u.Email, u.Phone, u.DateOfBirth, AccessRules.AgeOn(u.DateOfBirth, today),
        u.CountryCode, u.PhotoUrl, u.Bio, u.Languages, u.Interests, BadgeNames(u.VerificationBadges), u.Role,
        u.PasswordHash is not null, u.CreatedAt);

    /// <summary>Badges as stable names for the client: contactVerified, photoVerified, idVerified.</summary>
    public static IReadOnlyList<string> BadgeNames(VerificationBadges badges) =>
        Enum.GetValues<VerificationBadges>()
            .Where(b => b != VerificationBadges.None && badges.HasFlag(b))
            .Select(b => char.ToLowerInvariant(b.ToString()[0]) + b.ToString()[1..])
            .ToList();
}
