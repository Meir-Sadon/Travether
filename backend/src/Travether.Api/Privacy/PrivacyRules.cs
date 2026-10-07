using Travether.Api.Domain;

namespace Travether.Api.Privacy;

/// <summary>Retention periods and consent kinds (PLAN.md §4.9, docs/PRIVACY.md).</summary>
public static class PrivacyRules
{
    /// <summary>Accepted together at sign-up and again whenever <see cref="Auth.AuthOptions.LegalVersion"/> changes.</summary>
    public static readonly IReadOnlyList<ConsentKind> Legal = [ConsentKind.Terms, ConsentKind.PrivacyPolicy, ConsentKind.CommunityGuidelines];

    /// <summary>Off until the user opts in; withdrawable at any time.</summary>
    public static readonly IReadOnlyList<ConsentKind> Optional = [ConsentKind.Analytics, ConsentKind.MarketingEmail];

    public static readonly TimeSpan KeepNotifications = TimeSpan.FromDays(90);

    /// <summary>Spent and expired one-time codes are only kept long enough to enforce the hourly limit.</summary>
    public static readonly TimeSpan KeepLoginCodes = TimeSpan.FromDays(1);

    /// <summary>Device ids of accounts that are not banned (banned devices live on as hashes in banned_identifiers).</summary>
    public static readonly TimeSpan KeepDevices = TimeSpan.FromDays(180);

    public static string DeletedEmail(Guid userId) => $"deleted-{userId:N}@deleted.invalid";

    /// <summary>"ZZ" is the ISO user-assigned code for an unknown country and passes the column check.</summary>
    public const string DeletedCountry = "ZZ";
}
