namespace Travether.Api.Domain;

/// <summary>
/// A registered traveler. Field visibility (PLAN.md §4.1) is enforced by
/// <see cref="Authorization.AccessRules"/>, never by the client.
/// Credentials: an optional password hash here, Google/Apple links in <see cref="ExternalLogin"/>,
/// and one-time email codes in <see cref="LoginCode"/>.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>Public first name.</summary>
    public required string DisplayName { get; set; }

    /// <summary>Private: shown only to approved co-participants.</summary>
    public required string FullName { get; set; }

    /// <summary>Private. Stored lower-cased; unique among non-deleted users.</summary>
    public required string Email { get; set; }

    /// <summary>Private. E.164; the owner may share it in chat.</summary>
    public string? Phone { get; set; }

    /// <summary>Private; only the derived age is public. Must be 18+.</summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>ISO 3166-1 alpha-2, public (flag).</summary>
    public required string CountryCode { get; set; }

    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Interests { get; set; } = [];
    public VerificationBadges VerificationBadges { get; set; }
    public UserRole Role { get; set; }

    /// <summary>ASP.NET Core Identity hash (PBKDF2). Null for accounts that only use codes or Google/Apple.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Bumped on logout-everywhere, password change, ban and deletion. Session cookies carry the
    /// version they were issued with and stop working once it changes.
    /// </summary>
    public int SessionVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by moderation; a banned user is treated as absent by every access rule.</summary>
    public DateTimeOffset? BannedAt { get; set; }

    /// <summary>Soft delete marker; personal fields are scrubbed when it is set (step 1.12).</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
