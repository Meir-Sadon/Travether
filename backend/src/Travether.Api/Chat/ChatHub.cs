using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Travether.Api.Auth;

namespace Travether.Api.Chat;

/// <summary>
/// Push-only hub: messages are sent through the REST API, which checks access and then pushes a
/// <c>message</c> event to each person who may read it. Nobody joins rooms, so leaving a trip or plan
/// stops delivery at once.
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    public const string Path = "/hubs/chat";
}

/// <summary>Hub users are addressed by their Travether user id.</summary>
public sealed class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.GetUserId()?.ToString();
}
