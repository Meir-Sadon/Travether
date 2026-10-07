using System.ComponentModel.DataAnnotations;
using Travether.Api.Domain;

namespace Travether.Api.Privacy;

public sealed record ConsentRecordDto(ConsentKind Kind, string Version, DateTimeOffset GrantedAt, DateTimeOffset? WithdrawnAt);

/// <summary>
/// The signed-in user's privacy state. <c>NeedsConsent</c>: the terms, privacy policy or guidelines changed
/// since they last accepted them. <c>Analytics</c> and <c>MarketingEmail</c> are optional and off until granted.
/// </summary>
public sealed record PrivacyDto(string LegalVersion, bool NeedsConsent, bool Analytics, bool MarketingEmail, IReadOnlyList<ConsentRecordDto> History);

public sealed record OptionalConsentInput(ConsentKind Kind, bool Granted);

/// <summary>Deleting the account needs the password, or a code from <c>POST /api/me/delete/code</c>.</summary>
public sealed record DeleteAccountInput([MaxLength(128)] string? Password, [RegularExpression(@"^\s*\d{6}\s*$")] string? Code);
