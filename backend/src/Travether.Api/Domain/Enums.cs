namespace Travether.Api.Domain;

// Enums are stored as snake_case text (e.g. CoAdmin → "co_admin"); see Data/SnakeCaseEnumConverter.

public enum UserRole { Traveler, Moderator }

/// <summary>Earned profile badges (PLAN.md §4.1). Stored as an integer bit set.</summary>
[Flags]
public enum VerificationBadges
{
    None = 0,
    ContactVerified = 1,
    PhotoVerified = 2,
    IdVerified = 4,
}

public enum CardVisibility { Public, InviteOnly }

public enum CardRole { Owner, CoAdmin, Member }

/// <summary>A membership row is kept after leaving so history (chats, plans) stays attributable.</summary>
public enum MembershipStatus { Active, Left, Removed }

/// <summary>Join request lifecycle (PLAN.md §4.5), shared by card and plan requests.</summary>
public enum RequestStatus { Requested, Approved, Rejected, Withdrawn, Expired }

public enum PlanCategory { Hike, DayTrip, Food, Nightlife, Tour, Beach, Transport, Other }

public enum LocationPrecision { Exact, Regional }

public enum PlanAudience { Open, GroupsOnly }

public enum PlanStatus { Open, Full, Cancelled, Done }

public enum ConversationType { Card, Plan, Direct }

/// <summary>Text, or a phone/WhatsApp number the sender chose to share (PLAN.md decision 3).</summary>
public enum MessageKind { Text, ContactPhone, ContactWhatsapp }

public enum MeetAnswer { Met, Cancelled, NoShow }

public enum ReportTargetType { User, Card, Plan, Message, Review }

public enum ReportStatus { Open, Actioned, Dismissed }

public enum ConsentKind { Terms, PrivacyPolicy, CommunityGuidelines, MarketingEmail }
