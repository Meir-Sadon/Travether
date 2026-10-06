using Microsoft.EntityFrameworkCore;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Plans;

/// <summary>Expires open plan requests once the plan has started (PLAN.md §4.5). Runs every few minutes.</summary>
public sealed class PlanRequestSweeper(IServiceScopeFactory scopes, TimeProvider clock, ILogger<PlanRequestSweeper> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public static Task<int> ExpireStartedAsync(TravetherDbContext db, DateTimeOffset now, CancellationToken ct) =>
        db.PlanRequests
            .Where(r => r.Status == RequestStatus.Requested && r.Plan.StartsAt <= now)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, RequestStatus.Expired).SetProperty(r => r.DecidedAt, now), ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TravetherDbContext>();
                var expired = await ExpireStartedAsync(db, clock.GetUtcNow(), stoppingToken).ConfigureAwait(false);
                if (expired > 0 && log.IsEnabled(LogLevel.Information))
                {
                    log.LogInformation("Expired {Count} plan requests for plans that started", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Plan request sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
