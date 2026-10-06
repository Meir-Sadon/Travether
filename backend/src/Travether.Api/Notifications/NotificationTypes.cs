using System.Globalization;
using Travether.Api.Domain;

namespace Travether.Api.Notifications;

/// <summary>The settings toggles a notification type falls under (PLAN.md §4.7).</summary>
public enum NotificationCategory { Requests, Messages, Matches, Reminders, Reviews }

/// <summary>
/// What a notification points at. The app renders its own (translated) text from the type and these
/// fields; push and email use <see cref="NotificationTypes.Text"/>.
/// </summary>
public sealed record NotificationPayload(string Url, string? Actor = null, string? Subject = null, string? Preview = null, int? Count = null);

public static class NotificationTypes
{
    public const string CardRequest = "card_request";
    public const string CardRequestApproved = "card_request_approved";
    public const string CardRequestRejected = "card_request_rejected";
    public const string PlanRequest = "plan_request";
    public const string PlanRequestApproved = "plan_request_approved";
    public const string PlanRequestRejected = "plan_request_rejected";
    public const string PlanJoined = "plan_joined";
    public const string PlanCancelled = "plan_cancelled";
    public const string PlanReminder = "plan_reminder";
    public const string ChatMessage = "chat_message";
    public const string MatchesDigest = "matches_digest";

    public static NotificationCategory CategoryOf(string type) => type switch
    {
        ChatMessage => NotificationCategory.Messages,
        MatchesDigest => NotificationCategory.Matches,
        PlanReminder or PlanCancelled => NotificationCategory.Reminders,
        _ => NotificationCategory.Requests,
    };

    /// <summary>Chat has its own unread counts in the inbox, so chat pushes don't appear in the notification list.</summary>
    public static bool InApp(string type) => type != ChatMessage;

    /// <summary>Types that also go out by email (when the user keeps email on).</summary>
    public static bool Emails(string type) => type is CardRequest or CardRequestApproved or CardRequestRejected
        or PlanRequest or PlanRequestApproved or PlanRequestRejected or PlanCancelled or MatchesDigest;

    /// <summary>English push and email text. The app itself translates from the type and payload.</summary>
    public static (string Title, string Body) Text(string type, NotificationPayload p)
    {
        var actor = p.Actor ?? "Someone";
        var subject = p.Subject ?? "your trip";
        return type switch
        {
            CardRequest or PlanRequest => ("New join request", $"{actor} asked to join {subject}."),
            CardRequestApproved => ("You're in!", $"You joined {subject}."),
            PlanRequestApproved => ("You're going!", $"Your request for {subject} was approved."),
            CardRequestRejected or PlanRequestRejected => ("Join request declined", $"Your request to join {subject} wasn't accepted."),
            PlanJoined => ("Someone joined your plan", $"{actor} joined {subject}."),
            PlanCancelled => ("Plan cancelled", $"{subject} was cancelled."),
            PlanReminder => ($"Coming up: {subject}", string.Create(CultureInfo.InvariantCulture, $"Starts in about {p.Count ?? 2} hours.")),
            ChatMessage => (subject, p.Preview is { } preview ? $"{actor}: {preview}" : $"{actor} shared a contact number."),
            MatchesDigest => ($"New plans near {subject}", p.Count == 1 ? "1 new plan matches your trip." : string.Create(CultureInfo.InvariantCulture, $"{p.Count} new plans match your trip.")),
            _ => ("Travether", subject),
        };
    }
}

public static class NotificationRules
{
    public static bool Wants(NotificationSettings s, NotificationCategory category) => category switch
    {
        NotificationCategory.Requests => s.Requests,
        NotificationCategory.Messages => s.Messages,
        NotificationCategory.Matches => s.Matches,
        NotificationCategory.Reminders => s.Reminders,
        NotificationCategory.Reviews => s.Reviews,
        _ => true,
    };

    /// <summary>The user's local time now, falling back to UTC for an unknown zone.</summary>
    public static DateTimeOffset LocalNow(NotificationSettings s, DateTimeOffset now) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(s.TimeZoneId, out var zone) ? TimeZoneInfo.ConvertTime(now, zone) : now;

    /// <summary>True between QuietFrom and QuietTo local time; the range may wrap past midnight.</summary>
    public static bool InQuietHours(NotificationSettings s, DateTimeOffset now)
    {
        if (s.QuietFrom is not { } from || s.QuietTo is not { } to || from == to)
        {
            return false;
        }

        var local = TimeOnly.FromTimeSpan(LocalNow(s, now).TimeOfDay);
        return from < to ? local >= from && local < to : local >= from || local < to;
    }
}
