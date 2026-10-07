namespace Travether.Api.Telemetry;

public static class TelemetrySetup
{
    public static IServiceCollection AddTravetherTelemetry(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var options = config.GetSection("Telemetry").Get<TelemetryOptions>() ?? new TelemetryOptions();
        if (config["Telemetry:Environment"] is null)
        {
            options.Environment = env.EnvironmentName.ToLowerInvariant();
        }

        services.AddSingleton(options);
        return services;
    }

    /// <summary>
    /// Server errors go to Sentry when <c>Sentry:Dsn</c> is set. No request bodies, cookies, user ids or IP
    /// addresses are sent, and tracing is off.
    /// </summary>
    public static IWebHostBuilder UseTravetherSentry(this IWebHostBuilder host, IConfiguration config, IHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(config["Sentry:Dsn"]))
        {
            return host;
        }

        return host.UseSentry(o =>
        {
            o.Dsn = config["Sentry:Dsn"];
            o.Environment = config["Telemetry:Environment"] ?? env.EnvironmentName.ToLowerInvariant();
            o.SendDefaultPii = false;
            o.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
            o.TracesSampleRate = 0;
            o.SetBeforeSend(e =>
            {
                e.User = new Sentry.SentryUser();
                e.Request.Cookies = null;
                e.Request.QueryString = null;
                return e;
            });
        });
    }
}
