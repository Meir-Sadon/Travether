using Microsoft.EntityFrameworkCore;
using Npgsql;
using Travether.Api.Domain;

namespace Travether.Api.Tests;

/// <summary>Invariants the database enforces on its own, whatever the API does.</summary>
[Collection(DatabaseTests.Name)]
public sealed class SchemaTests(PostgresFixture pg)
{
    private static async Task<PostgresException> SaveFails(DbContext db)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return Assert.IsType<PostgresException>(ex.InnerException);
    }

    [Fact]
    public async Task Model_has_no_pending_changes()
    {
        await using var db = pg.CreateDbContext();
        Assert.False(db.Database.HasPendingModelChanges(), "Run `dotnet ef migrations add <Name>`; the model and migrations differ.");
    }

    [Fact]
    public async Task A_card_has_exactly_one_owner()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        db.CardMembers.Add(new CardMember { CardId = s.Card.Id, UserId = s.Olga.Id, Role = CardRole.Owner, Status = MembershipStatus.Active });

        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await SaveFails(db)).SqlState);
    }

    [Fact]
    public async Task Emails_are_unique_and_lower_case()
    {
        await using var db = pg.CreateDbContext();
        var a = Scenario.NewUser("Dup");
        var b = Scenario.NewUser("Dup");
        b.Email = a.Email;
        db.Users.AddRange(a, b);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await SaveFails(db)).SqlState);

        db.ChangeTracker.Clear();
        var upper = Scenario.NewUser("Upper");
        upper.Email = "Upper@Example.com";
        db.Users.Add(upper);
        Assert.Equal(PostgresErrorCodes.CheckViolation, (await SaveFails(db)).SqlState);
    }

    [Fact]
    public async Task A_deleted_account_frees_its_email()
    {
        await using var db = pg.CreateDbContext();
        var old = Scenario.NewUser("Old");
        old.DeletedAt = DateTimeOffset.UtcNow;
        var fresh = Scenario.NewUser("Fresh");
        fresh.Email = old.Email;
        db.Users.AddRange(old, fresh);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Card_dates_must_be_in_order()
    {
        await using var db = pg.CreateDbContext();
        var owner = Scenario.NewUser("Owner");
        var card = Scenario.NewCard(owner, CardVisibility.Public);
        card.EndsOn = card.StartsOn.AddDays(-1);
        db.Users.Add(owner);
        db.VacationCards.Add(card);

        var ex = await SaveFails(db);
        Assert.Equal("ck_vacation_cards_dates", ex.ConstraintName);
    }

    [Fact]
    public async Task Review_stars_are_1_to_5_and_not_for_yourself()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        db.Reviews.Add(new Review { Id = Guid.NewGuid(), PlanId = s.Hike.Id, ReviewerId = s.Bob.Id, RevieweeId = s.Alice.Id, Stars = 6 });
        Assert.Equal("ck_reviews_stars", (await SaveFails(db)).ConstraintName);

        db.ChangeTracker.Clear();
        db.Reviews.Add(new Review { Id = Guid.NewGuid(), PlanId = s.Hike.Id, ReviewerId = s.Bob.Id, RevieweeId = s.Bob.Id, Stars = 5 });
        Assert.Equal("ck_reviews_not_self", (await SaveFails(db)).ConstraintName);
    }

    [Fact]
    public async Task One_open_join_request_per_person()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        db.PlanRequests.Add(new PlanRequest { Id = Guid.NewGuid(), PlanId = s.Hike.Id, RequesterId = s.Olga.Id, Status = RequestStatus.Rejected });
        db.PlanRequests.Add(new PlanRequest { Id = Guid.NewGuid(), PlanId = s.Hike.Id, RequesterId = s.Olga.Id, Status = RequestStatus.Requested });
        await db.SaveChangesAsync(); // a rejected one doesn't block a new request

        db.PlanRequests.Add(new PlanRequest { Id = Guid.NewGuid(), PlanId = s.Hike.Id, RequesterId = s.Olga.Id, Status = RequestStatus.Requested });
        Assert.Equal(PostgresErrorCodes.UniqueViolation, (await SaveFails(db)).SqlState);
    }

    [Fact]
    public async Task Enums_are_stored_as_snake_case_text()
    {
        await using var db = pg.CreateDbContext();
        var s = await Scenario.SeedAsync(db);
        await using var conn = await pg.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT role FROM card_members WHERE card_id = @c AND user_id = @u", conn);
        cmd.Parameters.AddWithValue("c", s.Card.Id);
        cmd.Parameters.AddWithValue("u", s.Bob.Id);

        Assert.Equal("co_admin", await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Date_overlap_query_uses_the_daterange_index()
    {
        await using var conn = await pg.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT indexdef FROM pg_indexes WHERE indexname = 'ix_vacation_cards_dates'", conn);

        var def = (string?)await cmd.ExecuteScalarAsync();

        Assert.NotNull(def);
        Assert.Contains("gist", def, StringComparison.Ordinal);
        Assert.Contains("daterange(starts_on, ends_on, '[]'::text)", def, StringComparison.Ordinal);
    }
}
