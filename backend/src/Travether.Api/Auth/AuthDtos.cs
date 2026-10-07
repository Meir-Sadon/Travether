using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Auth;

public sealed record ProvidersDto(string? GoogleClientId, string? AppleClientId, string? AppleRedirectUri);

/// <summary>
/// <c>NeedsConsent</c>: the legal documents changed since the user last accepted them.
/// <c>Analytics</c>: the user opted in to product analytics.
/// </summary>
public sealed record SessionDto(MeDto? User, bool NeedsConsent = false, bool Analytics = false);

/// <summary>
/// Result of a sign-in step. <c>signedIn</c>: the cookie is set. <c>needsProfile</c>: the identity is
/// verified but no account exists yet; finish with <c>POST /api/auth/register</c> and the sign-up token.
/// </summary>
public sealed record AuthResultDto(string Status, MeDto? User = null, string? SignupToken = null, string? Email = null, string? SuggestedName = null)
{
    public static AuthResultDto SignedIn(MeDto user) => new("signedIn", user);

    public static AuthResultDto NeedsProfile(string token, string email, string? name) => new("needsProfile", null, token, email, name);
}

public sealed record EmailStartRequest([Required, EmailAddress, MaxLength(254)] string Email);

public sealed record EmailVerifyRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, RegularExpression(@"^\s*\d{6}\s*$")] string Code);

public sealed record ExternalSignInRequest(ExternalProvider Provider, [Required, MaxLength(8192)] string IdToken, [MaxLength(40)] string? GivenName);

public sealed record LoginRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, MaxLength(128)] string Password);

public sealed record RegisterRequest(
    string? SignupToken,
    [EmailAddress, MaxLength(254)] string? Email,
    [MinLength(AuthRules.MinPasswordLength), MaxLength(AuthRules.MaxPasswordLength)] string? Password,
    [Required, MaxLength(40)] string DisplayName,
    [Required, MaxLength(120)] string FullName,
    DateOnly DateOfBirth,
    [Required, RegularExpression("^[A-Z]{2}$")] string CountryCode,
    bool AcceptTerms,
    bool AllowAnalytics = false);

public sealed record PasswordResetRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression(@"^\s*\d{6}\s*$")] string Code,
    [Required, MinLength(AuthRules.MinPasswordLength), MaxLength(AuthRules.MaxPasswordLength)] string NewPassword);

public sealed record CodeRequest([Required, RegularExpression(@"^\s*\d{6}\s*$")] string Code);

public static class AuthRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
}
