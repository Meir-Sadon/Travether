namespace Travether.Api.Auth;

public sealed class AuthOptions
{
    /// <summary>
    /// HMAC key for session and sign-up tokens and for hashing one-time codes. At least 32 bytes.
    /// Required outside Development; the development fallback is not secret.
    /// </summary>
    public string? SigningKey { get; set; }

    public string Issuer { get; set; } = "travether";

    public int SessionDays { get; set; } = 30;

    /// <summary>Google OAuth web client id. Google sign-in is hidden when empty.</summary>
    public string? GoogleClientId { get; set; }

    /// <summary>Sign in with Apple services id. Apple sign-in is hidden when empty.</summary>
    public string? AppleClientId { get; set; }

    /// <summary>Redirect URI registered for the Apple services id (Apple's popup flow still requires one).</summary>
    public string? AppleRedirectUri { get; set; }

    /// <summary>Version of the terms, privacy policy and community guidelines accepted at sign-up.</summary>
    public string LegalVersion { get; set; } = "2026-10-01";
}
