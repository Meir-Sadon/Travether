using System.Security.Cryptography;
using Travether.Api.Authorization;

namespace Travether.Api.Cards;

public static class CardRules
{
    public const int MaxNameLength = 80;
    public const int MaxRegions = 5;
    public const int MaxRegionLength = 80;
    public const int MaxTripDays = 366;
    public const int SlugLength = 10;

    private const string SlugAlphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Unguessable share slug (~57 bits) without look-alike characters (0/O, 1/l/I).</summary>
    public static string NewSlug() => RandomNumberGenerator.GetString(SlugAlphabet, SlugLength);

    /// <summary>Trims, drops blanks and case-insensitive duplicates, keeps the order.</summary>
    public static List<string> NormalizeRegions(IEnumerable<string> regions) =>
        regions.Select(r => r.Trim()).Where(r => r.Length > 0).DistinctBy(r => r.ToUpperInvariant()).ToList();

    /// <summary>Returns an error code, or null when the card's fields are valid.</summary>
    public static string? Validate(string name, IReadOnlyList<string> regions, DateOnly startsOn, DateOnly endsOn, DateOnly today, bool isNew)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "NameRequired";
        }

        if (regions.Count == 0)
        {
            return "RegionsRequired";
        }

        if (regions.Count > MaxRegions || regions.Any(r => r.Length > MaxRegionLength))
        {
            return "InvalidRegions";
        }

        if (endsOn < startsOn)
        {
            return "DatesOutOfOrder";
        }

        if (endsOn.DayNumber - startsOn.DayNumber >= MaxTripDays)
        {
            return "TripTooLong";
        }

        // A new trip can't be over already (one day of slack for time zones).
        if (isNew && endsOn < today.AddDays(-1))
        {
            return "TripInPast";
        }

        return null;
    }

    public static string AccessName(CardAccess access) => access switch
    {
        CardAccess.Owner => "owner",
        CardAccess.CoAdmin => "coAdmin",
        CardAccess.Member => "member",
        _ => "preview",
    };
}
