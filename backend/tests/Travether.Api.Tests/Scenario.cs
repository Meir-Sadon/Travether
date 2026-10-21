using NetTopologySuite.Geometries;
using Travether.Api.Data;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

/// <summary>
/// A small trip seeded with fresh ids so tests can share one database:
/// Alice owns a card in Chiang Mai, Bob is a co-admin, Cleo a member. Alice hosts a hike that
/// Bob has joined. Olga is an outsider with her own card. Mallory has blocked Alice.
/// </summary>
public sealed class Scenario
{
    public static readonly Point MeetingPoint = new(98.98765, 18.78765) { SRID = 4326 };

    public User Alice { get; } = NewUser("Alice");
    public User Bob { get; } = NewUser("Bob");
    public User Cleo { get; } = NewUser("Cleo");
    public User Olga { get; } = NewUser("Olga");
    public User Mallory { get; } = NewUser("Mallory");
    public VacationCard Card { get; private set; } = null!;
    public VacationCard OlgaCard { get; private set; } = null!;
    public ActivityPlan Hike { get; private set; } = null!;
    public Conversation CardChat { get; private set; } = null!;
    public Conversation PlanChat { get; private set; } = null!;

    public static User NewUser(string name) => new()
    {
        Id = Guid.NewGuid(),
        DisplayName = name,
        FullName = $"{name} Example",
        Email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
        DateOfBirth = new DateOnly(1995, 5, 5),
        CountryCode = "IL",
    };

    public static VacationCard NewCard(User owner, CardVisibility visibility) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = owner.Id,
        Name = $"{owner.DisplayName}'s trip",
        CountryCode = "TH",
        Regions = ["Chiang Mai"],
        StartsOn = new DateOnly(2026, 10, 10),
        EndsOn = new DateOnly(2026, 10, 20),
        Visibility = visibility,
        ShareSlug = Guid.NewGuid().ToString("N")[..12],
        Members = [new CardMember { UserId = owner.Id, Role = CardRole.Owner, Status = MembershipStatus.Active }],
    };

    public static async Task<Scenario> SeedAsync(TravetherDbContext db, CardVisibility visibility = CardVisibility.InviteOnly)
    {
        var s = new Scenario();
        db.Users.AddRange(s.Alice, s.Bob, s.Cleo, s.Olga, s.Mallory);

        s.Card = NewCard(s.Alice, visibility);
        s.Card.Members.Add(new CardMember { UserId = s.Bob.Id, Role = CardRole.CoAdmin, Status = MembershipStatus.Active });
        s.Card.Members.Add(new CardMember { UserId = s.Cleo.Id, Role = CardRole.Member, Status = MembershipStatus.Active });
        s.OlgaCard = NewCard(s.Olga, CardVisibility.Public);
        db.VacationCards.AddRange(s.Card, s.OlgaCard);

        s.Hike = new ActivityPlan
        {
            Id = Guid.NewGuid(),
            CardId = s.Card.Id,
            HostId = s.Alice.Id,
            Title = "Sunrise hike to Doi Suthep",
            Category = PlanCategory.Hike,
            Origin = MeetingPoint,
            OriginAreaLabel = "Old City, Chiang Mai",
            Destination = "Wat Phra That Doi Suthep, gate 2",
            DestinationPrecision = LocationPrecision.Exact,
            // Npgsql writes timestamptz only from UTC offsets; the API converts local times before saving.
            StartsAt = new DateTimeOffset(2026, 10, 14, 5, 30, 0, TimeSpan.FromHours(7)).ToUniversalTime(),
            TimeZoneId = "Asia/Bangkok",
            SeatLimit = 6,
            Audience = PlanAudience.Open,
            Status = PlanStatus.Open,
            Participants =
            [
                new PlanParticipant { UserId = s.Alice.Id, SourceCardId = s.Card.Id, Status = MembershipStatus.Active },
                new PlanParticipant { UserId = s.Bob.Id, SourceCardId = s.Card.Id, Status = MembershipStatus.Active },
            ],
        };
        db.ActivityPlans.Add(s.Hike);

        s.CardChat = new Conversation { Id = Guid.NewGuid(), Type = ConversationType.Card, RefId = s.Card.Id };
        s.PlanChat = new Conversation { Id = Guid.NewGuid(), Type = ConversationType.Plan, RefId = s.Hike.Id };
        db.Conversations.AddRange(s.CardChat, s.PlanChat);

        db.Blocks.Add(new Block { BlockerId = s.Mallory.Id, BlockedId = s.Alice.Id });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return s;
    }
}
