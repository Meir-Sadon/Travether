using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;
using Travether.Api.Plans;

namespace Travether.Api.Chat;

public sealed record SenderDto(Guid Id, string DisplayName, string? PhotoUrl);

public sealed record MessageDto(Guid Id, SenderDto Sender, string Body, MessageKind Kind, DateTimeOffset CreatedAt);

/// <summary>A message pushed over the hub; <see cref="Chat"/> is the chat key, e.g. "plan-…".</summary>
public sealed record MessageEvent(string Chat, MessageDto Message);

public sealed record LastMessageDto(string SenderName, bool Mine, string Body, MessageKind Kind, DateTimeOffset CreatedAt);

/// <summary>A chat in the inbox list.</summary>
public sealed record ChatSummaryDto(string Key, ConversationType Type, Guid RefId, string Title, PlanCategory? Category, LastMessageDto? Last, int Unread, DateTimeOffset UpdatedAt);

/// <summary>A chat with its latest messages, oldest first.</summary>
public sealed record ChatDto(
    string Key, ConversationType Type, Guid RefId, string Title, int MemberCount, MeetingPointDto? MeetingPoint,
    IReadOnlyList<MessageDto> Messages, bool HasMore);

public sealed record SendMessageInput([MaxLength(2000)] string? Body);

public enum ContactKind { Phone, Whatsapp }

public sealed record ShareContactInput(ContactKind Kind);

public static class ChatKeys
{
    public static string For(ConversationType type, Guid refId) => $"{(type == ConversationType.Card ? "card" : "plan")}-{refId}";

    public static ConversationType? Parse(string kind) => kind switch
    {
        "card" => ConversationType.Card,
        "plan" => ConversationType.Plan,
        _ => null,
    };
}
