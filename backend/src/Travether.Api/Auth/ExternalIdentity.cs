using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Travether.Api.Domain;

namespace Travether.Api.Auth;

/// <summary>A verified identity from Google or Apple.</summary>
public sealed record ExternalIdentity(ExternalProvider Provider, string Subject, string Email, bool EmailVerified, string? Name);

/// <summary>Verifies an OpenID Connect ID token obtained by the browser from Google or Apple.</summary>
public interface IExternalIdentityVerifier
{
    bool IsEnabled(ExternalProvider provider);

    /// <summary>Null when the token is invalid, expired, for another app, or the provider is off.</summary>
    Task<ExternalIdentity?> VerifyAsync(ExternalProvider provider, string idToken, CancellationToken ct = default);
}

/// <summary>
/// Validates signature (provider JWKS via OIDC discovery, cached), issuer, audience (our client id)
/// and lifetime. The browser gets the ID token from Google Identity Services or Sign in with Apple JS.
/// </summary>
public sealed class OidcIdentityVerifier : IExternalIdentityVerifier
{
    private readonly AuthOptions options;
    private readonly JsonWebTokenHandler handler = new();
    private readonly ConfigurationManager<OpenIdConnectConfiguration> google;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> apple;

    public OidcIdentityVerifier(AuthOptions options, IHttpClientFactory httpFactory)
    {
        this.options = options;
        var retriever = new HttpDocumentRetriever(httpFactory.CreateClient("oidc")) { RequireHttps = true };
        google = new("https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(), retriever);
        apple = new("https://appleid.apple.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(), retriever);
    }

    public bool IsEnabled(ExternalProvider provider) => !string.IsNullOrWhiteSpace(ClientId(provider));

    public async Task<ExternalIdentity?> VerifyAsync(ExternalProvider provider, string idToken, CancellationToken ct = default)
    {
        var clientId = ClientId(provider);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var manager = provider == ExternalProvider.Google ? google : apple;
        var config = await manager.GetConfigurationAsync(ct).ConfigureAwait(false);
        var result = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = provider == ExternalProvider.Google ? ["https://accounts.google.com", "accounts.google.com"] : ["https://appleid.apple.com"],
            ValidAudience = clientId,
            IssuerSigningKeys = config.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        }).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return null;
        }

        var claims = result.Claims;
        if (!claims.TryGetValue("sub", out var sub) || !claims.TryGetValue("email", out var email))
        {
            return null;
        }

        // Google sends a boolean; Apple sends "true"/"false" strings.
        var verified = claims.TryGetValue("email_verified", out var v) && (v is true || (v as string) == "true");
        var name = claims.TryGetValue("given_name", out var given) ? given as string : null;
        return new ExternalIdentity(provider, (string)sub, (string)email, verified, name);
    }

    private string? ClientId(ExternalProvider provider) =>
        provider == ExternalProvider.Google ? options.GoogleClientId : options.AppleClientId;
}
