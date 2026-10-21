using Travether.Api.Domain;

namespace Travether.Api.Auth;

/// <summary>The session lives in an httpOnly, SameSite=Lax cookie; JavaScript never sees the token.</summary>
public sealed class SessionCookie(TokenService tokens)
{
    public const string Name = "tv_session";

    public void SignIn(HttpContext context, User user)
    {
        var token = tokens.CreateSession(user, out var expires);
        context.Response.Cookies.Append(Name, token, Options(context, expires));
    }

    public static void SignOut(HttpContext context) =>
        context.Response.Cookies.Delete(Name, Options(context, expires: null));

    private static CookieOptions Options(HttpContext context, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };
}
