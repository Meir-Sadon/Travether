using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Reviews;

/// <summary>Where reviewing someone from a plan stands, from the viewer's side.</summary>
public enum ReviewState
{
    /// <summary>Not both "Yes, we met" yet, and answers are still open. Doesn't reveal what the other person answered.</summary>
    Waiting,

    /// <summary>Both met; the viewer may review until <see cref="WrapUpPersonDto.ReviewUntil"/>.</summary>
    Open,

    Reviewed,

    /// <summary>The window closed, or the viewer said they didn't meet.</summary>
    Closed,
}

/// <summary>A review as others see it. A deleted or banned reviewer comes back as null ("Deleted user").</summary>
public sealed record ReviewDto(
    Guid Id, PersonDto? Reviewer, int Stars, string? Text, Guid PlanId, string PlanTitle, PlanCategory Category,
    DateTimeOffset CreatedAt, string? Reply, DateTimeOffset? RepliedAt);

public sealed record WrapUpPersonDto(PersonDto Person, ReviewState State, DateTimeOffset? ReviewUntil, ReviewDto? MyReview, bool TheyReviewedMe, ReviewDto? TheirReview);

/// <summary>"Did you meet?" and reviews for one finished plan (PLAN.md §4.6).</summary>
public sealed record WrapUpDto(
    Guid PlanId, string Title, PlanCategory Category, DateOnly LocalDate, MeetAnswer? MyAnswer, bool CanAnswer, DateTimeOffset AnswerUntil,
    IReadOnlyList<WrapUpPersonDto> People);

/// <summary>A finished plan still waiting for me: an answer, or reviews I can write.</summary>
public sealed record PendingWrapUpDto(Guid PlanId, string Title, PlanCategory Category, DateOnly LocalDate, bool NeedsAnswer, int ToReview);

public sealed record MeetInput(MeetAnswer Answer);

public sealed record ReviewInput(Guid RevieweeId, int Stars, [MaxLength(1000)] string? Text);

public sealed record ReplyInput([MaxLength(500)] string? Text);
