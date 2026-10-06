namespace Travether.Api.Notifications;

/// <summary>
/// Delivers queued notifications as soon as they are signalled (or every 30 s), and runs reminders
/// and the digest every 5 minutes. One API instance runs it; see docs/NOTIFICATIONS.md.
/// </summary>
public sealed partial class NotificationWorker(IServiceScopeFactory scopes, NotificationSignal signal, NotificationOptions options, TimeProvider clock, ILogger<NotificationWorker> log)
    : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JobsEvery = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.RunJobs)
        {
            return;
        }

        var nextJobs = clock.GetUtcNow();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var services = scope.ServiceProvider;
                if (clock.GetUtcNow() >= nextJobs)
                {
                    nextJobs = clock.GetUtcNow() + JobsEvery;
                    var jobs = services.GetRequiredService<NotificationJobs>();
                    await jobs.RemindAsync(stoppingToken).ConfigureAwait(false);
                    await jobs.DigestAsync(stoppingToken).ConfigureAwait(false);
                }

                var delivery = services.GetRequiredService<NotificationDelivery>();
                while (await delivery.DeliverPendingAsync(stoppingToken).ConfigureAwait(false) == NotificationDelivery.BatchSize)
                {
                    // A full batch: there may be more.
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFailed(log, ex);
            }

            try
            {
                await signal.WaitAsync(Poll, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Notification worker pass failed")]
    private static partial void LogFailed(ILogger logger, Exception ex);
}

public static class NotificationSetup
{
    public static IServiceCollection AddTravetherNotifications(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddSingleton(config.GetSection("Notifications").Get<NotificationOptions>() ?? new NotificationOptions());

        var push = config.GetSection("Push").Get<PushOptions>() ?? new PushOptions();
        if (!push.Enabled && env.IsDevelopment())
        {
            // Throwaway keys so push works locally; browsers must re-subscribe after a restart.
            (push.VapidPublicKey, push.VapidPrivateKey) = Vapid.Generate();
        }

        services.AddSingleton(push);
        services.AddHttpClient<IWebPushSender, WebPushSender>(http => http.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<NotificationSignal>();
        services.AddScoped<Notifier>();
        services.AddScoped<NotificationDelivery>();
        services.AddScoped<NotificationJobs>();
        services.AddHostedService<NotificationWorker>();
        return services;
    }
}
