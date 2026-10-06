using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;
using Travether.Api.Profiles;

namespace Travether.Api.Cards;

public sealed record CardInput(
    [Required, MaxLength(CardRules.MaxNameLength)] string Name,
    [Required, RegularExpression("^[A-Z]{2}$")] string CountryCode,
    [Required, MinLength(1), MaxLength(CardRules.MaxRegions)] IReadOnlyList<string> Regions,
    DateOnly StartsOn,
    DateOnly EndsOn,
    [MaxLength(1000)] string? Description,
    CardVisibility Visibility);

/// <summary>Partial update by the owner; only fields sent change.</summary>
public sealed record CardPatch(
    [MaxLength(CardRules.MaxNameLength)] string? Name,
    [RegularExpression("^[A-Z]{2}$")] string? CountryCode,
    [MinLength(1), MaxLength(CardRules.MaxRegions)] IReadOnlyList<string>? Regions,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    [MaxLength(1000)] string? Description,
    CardVisibility? Visibility);

public sealed record CardMemberDto(PersonDto Person, CardRole Role, DateTimeOffset JoinedAt);

/// <summary>
/// A Vacation Card as the viewer may see it (docs/AUTHORIZATION.md). Preview: name, destination,
/// dates, description, cover, member count. Members also get the member list and the share slug.
/// </summary>
public sealed record CardDto(
    Guid Id,
    string Name,
    string CountryCode,
    IReadOnlyList<string> Regions,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? Description,
    string? CoverUrl,
    CardVisibility Visibility,
    int MemberCount,
    string Access,
    string? ShareSlug,
    IReadOnlyList<CardMemberDto>? Members);

/// <summary>A card on the signed-in user's Home screen.</summary>
public sealed record MyCardDto(
    Guid Id,
    string Name,
    string CountryCode,
    IReadOnlyList<string> Regions,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? CoverUrl,
    CardVisibility Visibility,
    CardRole Role,
    int MemberCount,
    int PlanCount,
    IReadOnlyList<PersonDto> MembersPreview);
