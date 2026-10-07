using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Travether.Api.Auth;

namespace Travether.Api.Safety;

/// <summary>
/// Rate limits against spam and abuse (PLAN.md §4.8): per signed-in user for join requests, messages
/// and reports; per IP address for new accounts. Limits come from <c>RateLimits:*</c> settings.
/// </summary>
public static class SafetySetup
{
    public const string JoinRequestsRateLimit = "join-requests";
    public const string MessagesRateLimit = "messages";
    public const string ReportsRateLimit = "reports";
    public const string SignupRateLimit = "signup";

    public static IServiceCollection AddTravetherSafety(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<BanGuard>();

        var joinRequestsPerHour = config.GetValue("RateLimits:JoinRequestsPerHour", 20);
        var messagesPerMinute = config.GetValue("RateLimits:MessagesPerMinute", 30);
        var reportsPerDay = config.GetValue("RateLimits:ReportsPerDay", 20);

        // Generous: travelers in a hostel share one address.
        var signupsPerDay = config.GetValue("RateLimits:SignupsPerIpPerDay", 20);

        services.Configure<RateLimiterOptions>(limiter =>
        {
            limiter.AddPolicy(JoinRequestsRateLimit, http => PerUser(http, joinRequestsPerHour, TimeSpan.FromHours(1)));
            limiter.AddPolicy(MessagesRateLimit, http => PerUser(http, messagesPerMinute, TimeSpan.FromMinutes(1)));
            limiter.AddPolicy(ReportsRateLimit, http => PerUser(http, reportsPerDay, TimeSpan.FromDays(1)));
            limiter.AddPolicy(SignupRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey.For(http),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = signupsPerDay, Window = TimeSpan.FromDays(1) }));
        });
        return services;
    }

    private static RateLimitPartition<string> PerUser(HttpContext http, int limit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.User.GetUserId()?.ToString() ?? ClientKey.For(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = window });
}
