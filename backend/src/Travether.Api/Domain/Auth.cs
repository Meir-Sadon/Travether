namespace Travether.Api.Domain;

public enum ExternalProvider { Google, Apple }

/// <summary>A Google or Apple account linked to a user, keyed by the provider's stable subject id.</summary>
public sealed class ExternalLogin
{
    public ExternalProvider Provider { get; set; }

    /// <summary>The ID token's <c>sub</c> claim.</summary>
    public required string Subject { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public enum LoginCodePurpose { SignIn, VerifyEmail, ResetPassword }

/// <summary>
/// A six-digit one-time code sent by email. Only an HMAC of the code is stored. A code expires after
/// a few minutes, dies after too many wrong guesses, and works once.
/// </summary>
public sealed class LoginCode
{
    public Guid Id { get; set; }

    /// <summary>Lower-cased address the code was sent to.</summary>
    public required string Email { get; set; }

    public LoginCodePurpose Purpose { get; set; }
    public required string CodeHash { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}
