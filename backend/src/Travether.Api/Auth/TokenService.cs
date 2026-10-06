using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Travether.Api.Domain;

namespace Travether.Api.Auth;

/// <summary>
/// What a sign-up token proves: the email (and, for Google/Apple, the linked account) was verified
/// before the profile existed. The onboarding wizard sends it back to create the account.
/// </summary>
public sealed record SignupClaims(string Email, ExternalProvider? Provider, string? Subject, string? SuggestedName);

/// <summary>Signs the session cookie and the short-lived sign-up token (HMAC-SHA256).</summary>
public sealed class TokenService
{
    public const string SessionAudience = "travether-session";
    private const string SignupAudience = "travether-signup";
    private static readonly TimeSpan SignupLifetime = TimeSpan.FromMinutes(30);

    private readonly AuthOptions options;
    private readonly TimeProvider clock;
    private readonly JsonWebTokenHandler handler = new() { SetDefaultTimesOnTokenCreation = false };

    public TokenService(AuthOptions options, TimeProvider clock)
    {
        this.options = options;
        this.clock = clock;
        Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey!));
    }

    public SymmetricSecurityKey Key { get; }

    public TokenValidationParameters SessionValidation => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = SessionAudience,
        IssuerSigningKey = Key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "sub",
    };

    public string CreateSession(User user, out DateTimeOffset expires)
    {
        var now = clock.GetUtcNow();
        expires = now.AddDays(options.SessionDays);
        return Create(SessionAudience, now, expires, new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            [CurrentUser.SessionVersionClaim] = user.SessionVersion,
            [ClaimTypes.Role] = user.Role.ToString(),
        });
    }

    public string CreateSignup(SignupClaims claims)
    {
        var now = clock.GetUtcNow();
        var payload = new Dictionary<string, object> { ["email"] = claims.Email };
        if (claims.Provider is { } provider)
        {
            payload["provider"] = provider.ToString();
            payload["provider_sub"] = claims.Subject!;
        }

        if (claims.SuggestedName is { } name)
        {
            payload["name"] = name;
        }

        return Create(SignupAudience, now, now.Add(SignupLifetime), payload);
    }

    public async Task<SignupClaims?> ReadSignupAsync(string token)
    {
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = options.Issuer,
            ValidAudience = SignupAudience,
            IssuerSigningKey = Key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            LifetimeValidator = (notBefore, expires, _, _) => expires > clock.GetUtcNow().UtcDateTime,
        }).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return null;
        }

        var c = result.Claims;
        ExternalProvider? provider = c.TryGetValue("provider", out var p) && Enum.TryParse<ExternalProvider>(p as string, out var parsed) ? parsed : null;
        return new SignupClaims(
            (string)c["email"],
            provider,
            c.TryGetValue("provider_sub", out var s) ? s as string : null,
            c.TryGetValue("name", out var n) ? n as string : null);
    }

    private string Create(string audience, DateTimeOffset now, DateTimeOffset expires, Dictionary<string, object> claims) =>
        handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });
}
