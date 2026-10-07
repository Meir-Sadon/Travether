using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Travether.Api.Auth;
using Travether.Api.Data;

namespace Travether.Api.Chat;

/// <summary>
/// Push-only hub: messages are sent through the REST API, which checks access and then pushes a
/// <c>message</c> event to each person who may read it. Nobody joins rooms, so leaving a trip or plan
/// stops delivery at once.
/// </summary>
[Authorize]
public sealed class ChatHub(HubSessions sessions) : Hub
{
    public const string Path = "/hubs/chat";

    public override Task OnConnectedAsync()
    {
        sessions.Add(Context);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        sessions.Remove(Context);
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Hub users are addressed by their Travether user id.</summary>
public sealed class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.GetUserId()?.ToString();
}

/// <summary>
/// Open hub connections and the session they were opened with. The cookie is only checked when a connection
/// opens, so <see cref="SweepAsync"/> closes connections whose session has since ended (log out everywhere,
/// password reset, ban, account deletion).
/// </summary>
public sealed class HubSessions
{
    private readonly ConcurrentDictionary<string, HubCallerContext> open = new();

    public void Add(HubCallerContext context) => open[context.ConnectionId] = context;

    public void Remove(HubCallerContext context) => open.TryRemove(context.ConnectionId, out _);

    public int Count => open.Count;

    public async Task SweepAsync(TravetherDbContext db, CancellationToken ct)
    {
        var connections = open.Values.ToList();
        var userIds = connections.Select(c => c.User?.GetUserId()).OfType<Guid>().Distinct().ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        var live = await db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id) && u.BannedAt == null && u.DeletedAt == null)
            .ToDictionaryAsync(u => u.Id, u => u.SessionVersion.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        foreach (var connection in connections)
        {
            var userId = connection.User?.GetUserId();
            var version = connection.User?.FindFirst(CurrentUser.SessionVersionClaim)?.Value;
            if (userId is null || !live.TryGetValue(userId.Value, out var current) || current != version)
            {
                Remove(connection);
                connection.Abort();
            }
        }
    }
}

/// <summary>Runs <see cref="HubSessions.SweepAsync"/> every 30 seconds.</summary>
public sealed partial class HubSessionSweeper(HubSessions sessions, IServiceScopeFactory scopes, ILogger<HubSessionSweeper> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (sessions.Count == 0)
            {
                continue;
            }

            try
            {
                using var scope = scopes.CreateScope();
                await sessions.SweepAsync(scope.ServiceProvider.GetRequiredService<TravetherDbContext>(), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSweepFailed(logger, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Hub session sweep failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);
}
