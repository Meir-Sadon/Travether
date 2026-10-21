namespace Travether.Api.Domain;

/// <summary>A chat room. Card and plan rooms point at their card/plan through <see cref="RefId"/>.</summary>
public sealed class Conversation
{
    public Guid Id { get; set; }
    public ConversationType Type { get; set; }

    /// <summary>Card id or plan id; null for direct conversations.</summary>
    public Guid? RefId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Only used by direct conversations; card and plan rooms derive members from memberships.</summary>
    public List<ConversationMember> Members { get; set; } = [];
}

public sealed class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by moderation; hidden messages are not returned to participants.</summary>
    public DateTimeOffset? HiddenAt { get; set; }
}

public sealed class MeetConfirmation
{
    public Guid PlanId { get; set; }
    public Guid UserId { get; set; }
    public MeetAnswer Answer { get; set; }
    public DateTimeOffset AnsweredAt { get; set; }
}

/// <summary>Double-blind review of one person by another after a plan (PLAN.md §4.6).</summary>
public sealed class Review
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid ReviewerId { get; set; }
    public User Reviewer { get; set; } = null!;
    public Guid RevieweeId { get; set; }
    public User Reviewee { get; set; } = null!;

    /// <summary>1–5.</summary>
    public int Stars { get; set; }

    public string? Text { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the counterpart review arrives or the window closes; until then only the author sees it.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>The reviewee's single public reply.</summary>
    public string? ReplyText { get; set; }

    public DateTimeOffset? RepliedAt { get; set; }
}

public sealed class Report
{
    public Guid Id { get; set; }
    public Guid ReporterId { get; set; }
    public ReportTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public required string Reason { get; set; }
    public ReportStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Statement of reasons sent to the affected user when content is removed (EU DSA).</summary>
    public string? ResolutionNote { get; set; }
}

public sealed class Block
{
    public Guid BlockerId { get; set; }
    public Guid BlockedId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Type { get; set; }

    /// <summary>Type-specific data as JSON (jsonb column).</summary>
    public required string Payload { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Endpoint { get; set; }
    public required string P256dh { get; set; }
    public required string Auth { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Consent record (GDPR / Israeli PPL Amendment 13, PLAN.md §4.9): what was accepted, which version, when.</summary>
public sealed class Consent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ConsentKind Kind { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset? WithdrawnAt { get; set; }
}
