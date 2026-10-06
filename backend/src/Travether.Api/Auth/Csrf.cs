namespace Travether.Api.Auth;

/// <summary>
/// CSRF guard for cookie sessions: every state-changing API request must carry a custom header.
/// Browsers can't add custom headers to cross-site form posts, and cross-origin fetches with one
/// need a CORS preflight that this API never grants. The session cookie is also SameSite=Lax.
/// </summary>
public sealed class CsrfMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Travether-Csrf";

    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api")
            && !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method)
            && !HttpMethods.IsOptions(request.Method)
            && !request.Headers.ContainsKey(HeaderName))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new { code = "CsrfHeaderMissing" });
        }

        return next(context);
    }
}
