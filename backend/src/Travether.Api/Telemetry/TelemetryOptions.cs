namespace Travether.Api.Telemetry;

/// <summary>
/// Product analytics (PostHog, EU) and error tracking (Sentry), PLAN.md §6 and §9. Both are off while
/// their keys are empty. See docs/ANALYTICS.md.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>PostHog project API key (public by design; it can only send events).</summary>
    public string? PostHogKey { get; set; }

    public string PostHogHost { get; set; } = "https://eu.i.posthog.com";

    /// <summary>Sentry DSN for the browser app. The API reads its own from <c>Sentry:Dsn</c>.</summary>
    public string? SentryDsn { get; set; }

    /// <summary>Tags events so staging and production stay apart.</summary>
    public string Environment { get; set; } = "development";
}

/// <summary>What the browser needs to start analytics and error tracking. Read at runtime, so keys change without a rebuild.</summary>
public sealed record ClientConfigDto(string? PostHogKey, string PostHogHost, string? SentryDsn, string Environment);
