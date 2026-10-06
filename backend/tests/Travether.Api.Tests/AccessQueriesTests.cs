using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Travether.Api.Authorization;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

[Collection(DatabaseTests.Name)]
public sealed class AccessQueriesTests(PostgresFixture pg)
{
    [Fact]
    public async Task Card_access_follows_membership_visibility_and_the_share_link()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db, CardVisibility.InviteOnly);
        var q = new AccessQueries(db);

        Assert.Equal(CardAccess.Owner, await q.GetCardAccessAsync(s.Alice.Id, s.Card.Id));
        Assert.Equal(CardAccess.CoAdmin, await q.GetCardAccessAsync(s.Bob.Id, s.Card.Id));
        Assert.Equal(CardAccess.Member, await q.GetCardAccessAsync(s.Cleo.Id, s.Card.Id));
        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Olga.Id, s.Card.Id));
        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(null, s.Card.Id));
        Assert.Equal(CardAccess.Preview, await q.GetCardAccessAsync(s.Olga.Id, s.Card.Id, s.Card.ShareSlug));
        Assert.Equal(CardAccess.Preview, await q.GetCardAccessAsync(null, s.Card.Id, s.Card.ShareSlug));
        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Olga.Id, s.Card.Id, "wrong-slug"));
        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Mallory.Id, s.Card.Id, s.Card.ShareSlug));
    }

    [Fact]
    public async Task Members_who_left_lose_access()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        await db.CardMembers.Where(m => m.CardId == s.Card.Id && m.UserId == s.Cleo.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, MembershipStatus.Left));
        var q = new AccessQueries(db);

        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Cleo.Id, s.Card.Id));
        Assert.False(await q.CanReadConversationAsync(s.Cleo.Id, s.CardChat.Id));
    }

    [Fact]
    public async Task Banned_and_deleted_viewers_are_treated_as_visitors()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        await db.Users.Where(u => u.Id == s.Bob.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.BannedAt, DateTimeOffset.UtcNow));
        await db.Users.Where(u => u.Id == s.Cleo.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.DeletedAt, DateTimeOffset.UtcNow));
        var q = new AccessQueries(db);

        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Bob.Id, s.Card.Id));
        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Cleo.Id, s.Card.Id));
        Assert.Equal(ProfileAccess.None, await q.GetProfileAccessAsync(s.Alice.Id, s.Bob.Id));
    }

    [Fact]
    public async Task Exact_meeting_point_is_only_revealed_to_participants()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        var q = new AccessQueries(db);
        var from = new Point(98.9935, 18.7883) { SRID = 4326 }; // Tha Phae Gate, ~600 m from the meeting point

        var host = await q.GetPlanLocationAsync(s.Alice.Id, s.Hike.Id, from);
        var participant = await q.GetPlanLocationAsync(s.Bob.Id, s.Hike.Id, from);
        var cardMember = await q.GetPlanLocationAsync(s.Cleo.Id, s.Hike.Id, from);
        var outsider = await q.GetPlanLocationAsync(s.Olga.Id, s.Hike.Id, from);
        var visitor = await q.GetPlanLocationAsync(null, s.Hike.Id, null);
        var blocked = await q.GetPlanLocationAsync(s.Mallory.Id, s.Hike.Id, from);

        Assert.NotNull(host!.ExactOrigin);
        Assert.Equal(Scenario.MeetingPoint.X, participant!.ExactOrigin!.X, 6);
        Assert.Equal("Wat Phra That Doi Suthep, gate 2", participant.Destination);

        foreach (var view in new[] { cardMember, outsider, visitor })
        {
            Assert.NotNull(view);
            Assert.Null(view.ExactOrigin);
            Assert.Null(view.Destination); // the destination is exact, so it's private too
            Assert.Equal("Old City, Chiang Mai", view.AreaLabel);
        }

        Assert.Equal(new ApproxDistance(1, UnderOneKm: true), participant.Distance);
        Assert.NotNull(outsider!.Distance);
        Assert.Null(visitor!.Distance);
        Assert.Null(blocked);
    }

    [Fact]
    public async Task Public_distance_is_measured_to_the_grid_snapped_point()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);

        var snapped = await db.ActivityPlans.Where(p => p.Id == s.Hike.Id).Select(p => p.OriginPublic).SingleAsync();

        Assert.Equal(98.99, snapped.X, 6);
        Assert.Equal(18.79, snapped.Y, 6);
    }

    [Fact]
    public async Task Plan_access_levels()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        var q = new AccessQueries(db);

        Assert.Equal((PlanAccess.Host, CardAccess.Owner), await q.GetPlanAccessAsync(s.Alice.Id, s.Hike.Id));
        Assert.Equal((PlanAccess.Participant, CardAccess.CoAdmin), await q.GetPlanAccessAsync(s.Bob.Id, s.Hike.Id));
        Assert.Equal((PlanAccess.Public, CardAccess.Member), await q.GetPlanAccessAsync(s.Cleo.Id, s.Hike.Id));
        Assert.Equal((PlanAccess.Public, CardAccess.None), await q.GetPlanAccessAsync(s.Olga.Id, s.Hike.Id));
        Assert.Equal((PlanAccess.Public, CardAccess.None), await q.GetPlanAccessAsync(null, s.Hike.Id));
        Assert.Equal(PlanAccess.None, (await q.GetPlanAccessAsync(s.Mallory.Id, s.Hike.Id)).Plan);
    }

    [Fact]
    public async Task Deleted_plans_and_cards_are_invisible()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        await db.VacationCards.Where(c => c.Id == s.Card.Id).ExecuteUpdateAsync(u => u.SetProperty(c => c.DeletedAt, DateTimeOffset.UtcNow));
        var q = new AccessQueries(db);

        Assert.Equal(CardAccess.None, await q.GetCardAccessAsync(s.Alice.Id, s.Card.Id));
        Assert.Equal(PlanAccess.None, (await q.GetPlanAccessAsync(s.Bob.Id, s.Hike.Id)).Plan);
        Assert.Null(await q.GetPlanLocationAsync(s.Bob.Id, s.Hike.Id, null));
    }

    [Fact]
    public async Task Chats_are_readable_only_by_their_members()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        var q = new AccessQueries(db);

        Assert.True(await q.CanReadConversationAsync(s.Cleo.Id, s.CardChat.Id));
        Assert.False(await q.CanReadConversationAsync(s.Olga.Id, s.CardChat.Id));
        Assert.False(await q.CanReadConversationAsync(null, s.CardChat.Id));

        Assert.True(await q.CanReadConversationAsync(s.Bob.Id, s.PlanChat.Id));
        Assert.False(await q.CanReadConversationAsync(s.Cleo.Id, s.PlanChat.Id)); // card member, not a participant
    }

    [Fact]
    public async Task Direct_chats_close_when_either_side_blocks()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        var direct = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            Members = [new ConversationMember { UserId = s.Olga.Id }, new ConversationMember { UserId = s.Cleo.Id }],
        };
        db.Conversations.Add(direct);
        await db.SaveChangesAsync();
        var q = new AccessQueries(db);

        Assert.True(await q.CanReadConversationAsync(s.Olga.Id, direct.Id));
        Assert.False(await q.CanReadConversationAsync(s.Bob.Id, direct.Id));

        db.Blocks.Add(new Block { BlockerId = s.Cleo.Id, BlockedId = s.Olga.Id });
        await db.SaveChangesAsync();
        Assert.False(await q.CanReadConversationAsync(s.Olga.Id, direct.Id));
    }

    [Fact]
    public async Task Hidden_messages_and_messages_from_blocked_people_are_filtered()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        db.Messages.AddRange(
            new Message { Id = Guid.NewGuid(), ConversationId = s.CardChat.Id, SenderId = s.Alice.Id, Body = "Hi all" },
            new Message { Id = Guid.NewGuid(), ConversationId = s.CardChat.Id, SenderId = s.Bob.Id, Body = "spam", HiddenAt = DateTimeOffset.UtcNow },
            new Message { Id = Guid.NewGuid(), ConversationId = s.CardChat.Id, SenderId = s.Cleo.Id, Body = "From Cleo" });
        db.Blocks.Add(new Block { BlockerId = s.Bob.Id, BlockedId = s.Cleo.Id });
        await db.SaveChangesAsync();
        var q = new AccessQueries(db);

        var forBob = await q.VisibleMessages(s.Bob.Id, s.CardChat.Id).Select(m => m.Body).ToListAsync();
        var forAlice = await q.VisibleMessages(s.Alice.Id, s.CardChat.Id).Select(m => m.Body).ToListAsync();

        Assert.Equal(["Hi all"], forBob);
        Assert.Equal(["From Cleo", "Hi all"], forAlice.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Full_name_is_for_co_participants_only()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        var q = new AccessQueries(db);

        Assert.Equal(ProfileAccess.CoParticipant, await q.GetProfileAccessAsync(s.Cleo.Id, s.Alice.Id)); // same card
        Assert.Equal(ProfileAccess.Public, await q.GetProfileAccessAsync(s.Olga.Id, s.Alice.Id));
        Assert.Equal(ProfileAccess.Public, await q.GetProfileAccessAsync(null, s.Alice.Id));
        Assert.Equal(ProfileAccess.None, await q.GetProfileAccessAsync(s.Mallory.Id, s.Alice.Id));
        Assert.Equal(ProfileAccess.Self, await q.GetProfileAccessAsync(s.Alice.Id, s.Alice.Id));

        // Joining the same plan from different cards also counts.
        db.PlanParticipants.Add(new PlanParticipant { PlanId = s.Hike.Id, UserId = s.Olga.Id, SourceCardId = s.OlgaCard.Id, Status = MembershipStatus.Active });
        await db.SaveChangesAsync();
        Assert.Equal(ProfileAccess.CoParticipant, await q.GetProfileAccessAsync(s.Olga.Id, s.Bob.Id));
    }
}
