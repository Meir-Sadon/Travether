using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Travether.Api.Auth;

namespace Travether.Api.Privacy;

public static class PrivacySetup
{
    public const string ExportRateLimit = "export";

    public static IServiceCollection AddTravetherPrivacy(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<PrivacyService>();
        var exportsPerDay = config.GetValue("RateLimits:ExportsPerDay", 5);
        services.Configure<RateLimiterOptions>(limiter =>
            limiter.AddPolicy(ExportRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.User.GetUserId()?.ToString() ?? http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = exportsPerDay, Window = TimeSpan.FromDays(1) })));
        return services;
    }
}
