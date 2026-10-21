using System.Text.RegularExpressions;

namespace Travether.Api.Profiles;

public static partial class ProfileRules
{
    public const int MaxLanguages = 10;
    public const int MaxInterests = 12;

    /// <summary>Interest and travel-style tags the client offers (PLAN.md §4.1). Unknown tags are rejected.</summary>
    public static readonly IReadOnlySet<string> Interests = new HashSet<string>(StringComparer.Ordinal)
    {
        "hiking", "food", "nightlife", "culture", "beach", "dayTrips", "diving", "budget",
        "photography", "museums", "music", "sports", "wellness", "roadTrips", "camping", "shopping",
    };

    /// <summary>ISO 639-1 codes, lower-cased, de-duplicated, order kept.</summary>
    public static bool TryNormalizeLanguages(IEnumerable<string> input, out List<string> languages)
    {
        languages = input.Select(l => l.Trim().ToLowerInvariant()).Distinct().ToList();
        return languages.Count <= MaxLanguages && languages.All(l => LanguageCode().IsMatch(l));
    }

    public static bool TryNormalizeInterests(IEnumerable<string> input, out List<string> interests)
    {
        interests = input.Select(i => i.Trim()).Distinct().ToList();
        return interests.Count <= MaxInterests && interests.All(Interests.Contains);
    }

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex LanguageCode();
}
