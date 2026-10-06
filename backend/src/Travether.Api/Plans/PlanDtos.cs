using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Plans;

public sealed record LatLng(double Lat, double Lng);

/// <summary>The exact meeting point; participants only.</summary>
public sealed record MeetingPointDto(string Name, double Lat, double Lng);

public sealed record PlanInput(
    [MaxLength(80)] string Title,
    PlanCategory Category,
    LatLng Origin,
    [MaxLength(200)] string OriginName,
    [MaxLength(120)] string OriginAreaLabel,
    [MaxLength(200)] string Destination,
    LocationPrecision DestinationPrecision,
    DateOnly Date,
    TimeOnly Time,
    [MaxLength(1000)] string? Purpose,
    int SeatLimit,
    PlanAudience Audience,
    IReadOnlyList<Guid>? ParticipantIds);

/// <summary>Partial update; null keeps the current value. Date and Time are local to the meeting point.</summary>
public sealed record PlanPatch(
    [MaxLength(80)] string? Title,
    PlanCategory? Category,
    LatLng? Origin,
    [MaxLength(200)] string? OriginName,
    [MaxLength(120)] string? OriginAreaLabel,
    [MaxLength(200)] string? Destination,
    LocationPrecision? DestinationPrecision,
    DateOnly? Date,
    TimeOnly? Time,
    [MaxLength(1000)] string? Purpose,
    int? SeatLimit,
    PlanAudience? Audience);

public sealed record DistanceDto(int Km, bool UnderOneKm);

/// <summary>
/// A plan as the viewer may see it. <see cref="MeetingPoint"/> and an exact <see cref="Destination"/> arrive only for
/// participants; <see cref="Participants"/> only for participants and members of the plan's card.
/// </summary>
public sealed record PlanDto(
    Guid Id,
    Guid CardId,
    string? CardName,
    string Title,
    PlanCategory Category,
    DateTimeOffset StartsAt,
    string TimeZoneId,
    DateOnly LocalDate,
    TimeOnly LocalTime,
    string AreaLabel,
    DistanceDto? Distance,
    MeetingPointDto? MeetingPoint,
    string? Destination,
    LocationPrecision DestinationPrecision,
    string? Purpose,
    int SeatLimit,
    int SeatsTaken,
    PlanAudience Audience,
    PlanStatus Status,
    string Access,
    bool CanManage,
    bool CanSelfJoin,
    PersonDto Host,
    IReadOnlyList<PersonDto>? Participants,
    MyPlanRequestDto? MyRequest,
    int? PendingRequestCount);

/// <summary>A plan in a list (the card's plans tab, Discover).</summary>
public sealed record PlanSummaryDto(
    Guid Id,
    string Title,
    PlanCategory Category,
    DateTimeOffset StartsAt,
    string TimeZoneId,
    DateOnly LocalDate,
    TimeOnly LocalTime,
    string AreaLabel,
    int SeatLimit,
    int SeatsTaken,
    PlanAudience Audience,
    PlanStatus Status,
    PersonDto Host,
    bool Joined);

public sealed record PlanJoinRequestInput([MaxLength(300)] string? Message, Guid? SourceCardId, IReadOnlyList<Guid>? PartyUserIds);

/// <summary>The viewer's latest request to a plan, for the status stepper (PLAN.md §4.5).</summary>
public sealed record MyPlanRequestDto(Guid Id, RequestStatus Status, int PartySize, DateTimeOffset CreatedAt);

/// <summary>An open request as the host and the card's admins see it.</summary>
public sealed record PlanRequestDto(
    Guid Id, PersonDto Requester, IReadOnlyList<PersonDto> Party, string? SourceCardName, string? Message, RequestStatus Status, DateTimeOffset CreatedAt);
