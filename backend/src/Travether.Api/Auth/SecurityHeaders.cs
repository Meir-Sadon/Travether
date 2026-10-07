namespace Travether.Api.Auth;

/// <summary>
/// Browser hardening headers on every response. The policy forbids framing (clickjacking), plugins and
/// &lt;base&gt; rewrites. A script/connect allow-list is a follow-up: it must be tried against Google and Apple
/// sign-in, Sentry and PostHog on a deployed build first (docs/SECURITY_REVIEW.md).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ContentSecurityPolicy = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
        return next(context);
    }
}
