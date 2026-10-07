using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Data;

namespace Travether.Api.Auth;

public static class AuthSetup
{
    public const string AuthRateLimit = "auth";

    /// <summary>Used only in Development and tests, so a fresh clone runs without secrets.</summary>
    private const string DevelopmentSigningKey = "travether-development-signing-key-not-secret-0123456789";

    public static IServiceCollection AddTravetherAuth(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var options = config.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
        options.SigningKey ??= config["Jwt:Secret"]; // Render generates Jwt__Secret (render.yaml)
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            options.SigningKey = env.IsDevelopment() || env.IsEnvironment("Testing")
                ? DevelopmentSigningKey
                : throw new InvalidOperationException("Jwt:Secret (or Auth:SigningKey) is not set.");
        }

        if (options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Auth:SigningKey must be at least 32 characters.");
        }

        services.AddSingleton(options);
        services.AddSingleton<TokenService>();
        services.AddSingleton<SessionCookie>();
        services.AddScoped<LoginCodeService>();
        services.AddHttpClient("oidc");
        services.AddSingleton<IExternalIdentityVerifier, OidcIdentityVerifier>();

        var tokens = new TokenService(options, TimeProvider.System);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = tokens.SessionValidation;
                jwt.Events = new JwtBearerEvents
                {
                    // The token comes from the httpOnly cookie, never from an Authorization header.
                    OnMessageReceived = ctx =>
                    {
                        ctx.Token = ctx.Request.Cookies[SessionCookie.Name];
                        return Task.CompletedTask;
                    },

                    // Banned, deleted, or signed out everywhere: the cookie stops working at once.
                    OnTokenValidated = async ctx =>
                    {
                        var userId = ctx.Principal!.GetUserId();
                        var version = ctx.Principal!.FindFirst(CurrentUser.SessionVersionClaim)?.Value;
                        var db = ctx.HttpContext.RequestServices.GetRequiredService<TravetherDbContext>();
                        var user = userId is null ? null : await db.Users.AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new { u.SessionVersion, u.BannedAt, u.DeletedAt })
                            .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted).ConfigureAwait(false);
                        if (user is null || user.BannedAt is not null || user.DeletedAt is not null
                            || version != user.SessionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
                        {
                            ctx.Fail("Session is no longer valid.");
                        }
                    },
                };
            });
        services.AddAuthorization();

        var perMinute = config.GetValue("RateLimits:AuthPerMinute", 20);
        var placesPerMinute = config.GetValue("RateLimits:PlacesPerMinute", 60);
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsJsonAsync(new { code = "TooManyRequests" }, ct));
            limiter.AddPolicy(AuthRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey.For(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) }));
            limiter.AddPolicy(Places.PlaceSetup.RateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.User.GetUserId()?.ToString() ?? ClientKey.For(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = placesPerMinute, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }
}
