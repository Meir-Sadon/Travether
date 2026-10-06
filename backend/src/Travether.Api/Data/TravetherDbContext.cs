using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Travether.Api.Authorization;
using Travether.Api.Domain;

namespace Travether.Api.Data;

/// <summary>
/// EF Core model for PLAN.md §6.1. Tables and columns are snake_case (EFCore.NamingConventions),
/// enums are snake_case text with CHECK constraints, locations are PostGIS geography(Point, 4326).
/// </summary>
public sealed class TravetherDbContext(DbContextOptions<TravetherDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<VacationCard> VacationCards => Set<VacationCard>();
    public DbSet<CardMember> CardMembers => Set<CardMember>();
    public DbSet<CardRequest> CardRequests => Set<CardRequest>();
    public DbSet<ActivityPlan> ActivityPlans => Set<ActivityPlan>();
    public DbSet<PlanParticipant> PlanParticipants => Set<PlanParticipant>();
    public DbSet<PlanRequest> PlanRequests => Set<PlanRequest>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MeetConfirmation> MeetConfirmations => Set<MeetConfirmation>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Consent> Consents => Set<Consent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<UserRole>().HaveConversion<SnakeCaseEnumConverter<UserRole>>();
        configurationBuilder.Properties<CardVisibility>().HaveConversion<SnakeCaseEnumConverter<CardVisibility>>();
        configurationBuilder.Properties<CardRole>().HaveConversion<SnakeCaseEnumConverter<CardRole>>();
        configurationBuilder.Properties<MembershipStatus>().HaveConversion<SnakeCaseEnumConverter<MembershipStatus>>();
        configurationBuilder.Properties<RequestStatus>().HaveConversion<SnakeCaseEnumConverter<RequestStatus>>();
        configurationBuilder.Properties<PlanCategory>().HaveConversion<SnakeCaseEnumConverter<PlanCategory>>();
        configurationBuilder.Properties<LocationPrecision>().HaveConversion<SnakeCaseEnumConverter<LocationPrecision>>();
        configurationBuilder.Properties<PlanAudience>().HaveConversion<SnakeCaseEnumConverter<PlanAudience>>();
        configurationBuilder.Properties<PlanStatus>().HaveConversion<SnakeCaseEnumConverter<PlanStatus>>();
        configurationBuilder.Properties<ConversationType>().HaveConversion<SnakeCaseEnumConverter<ConversationType>>();
        configurationBuilder.Properties<MeetAnswer>().HaveConversion<SnakeCaseEnumConverter<MeetAnswer>>();
        configurationBuilder.Properties<ReportTargetType>().HaveConversion<SnakeCaseEnumConverter<ReportTargetType>>();
        configurationBuilder.Properties<ReportStatus>().HaveConversion<SnakeCaseEnumConverter<ReportStatus>>();
        configurationBuilder.Properties<ConsentKind>().HaveConversion<SnakeCaseEnumConverter<ConsentKind>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users", t =>
            {
                t.HasCheckConstraint("ck_users_country_code", "country_code ~ '^[A-Z]{2}$'");
                t.HasCheckConstraint("ck_users_email_lower", "email = lower(email)");
                InCheck(t, "role", EnumText.AllDbValues<UserRole>());
            });
            e.Property(x => x.DisplayName).HasMaxLength(40);
            e.Property(x => x.FullName).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Bio).HasMaxLength(500);
            e.Property(x => x.Role).HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // A deleted account frees its email for a new sign-up.
            e.HasIndex(x => x.Email).IsUnique().HasFilter("deleted_at IS NULL");
            // No global filter on users: messages and reviews by a deleted account stay visible
            // (as "Deleted user"). Access checks use AccessRules.IsActive instead.
        });

        modelBuilder.Entity<VacationCard>(e =>
        {
            e.ToTable("vacation_cards", t =>
            {
                t.HasCheckConstraint("ck_vacation_cards_dates", "ends_on >= starts_on");
                t.HasCheckConstraint("ck_vacation_cards_regions", "cardinality(regions) >= 1");
                t.HasCheckConstraint("ck_vacation_cards_country_code", "country_code ~ '^[A-Z]{2}$'");
                InCheck(t, "visibility", EnumText.AllDbValues<CardVisibility>());
            });
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Visibility).HasMaxLength(16);
            e.Property(x => x.ShareSlug).HasMaxLength(32);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.ShareSlug).IsUnique();
            e.HasIndex(x => x.OwnerId);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.DeletedAt == null);
            // GiST index on daterange(starts_on, ends_on, '[]') is created in the migration (expression index).
        });

        modelBuilder.Entity<CardMember>(e =>
        {
            e.ToTable("card_members", t =>
            {
                InCheck(t, "role", EnumText.AllDbValues<CardRole>());
                InCheck(t, "status", EnumText.AllDbValues<MembershipStatus>());
            });
            e.HasKey(x => new { x.CardId, x.UserId });
            e.Property(x => x.Role).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.UserId);
            // Exactly one owner per card.
            e.HasIndex(x => x.CardId).IsUnique().HasFilter("role = 'owner'").HasDatabaseName("ux_card_members_one_owner");
            e.HasOne(x => x.Card).WithMany(c => c.Members).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.Card.DeletedAt == null && x.User.DeletedAt == null);
        });

        modelBuilder.Entity<CardRequest>(e =>
        {
            e.ToTable("card_requests", t => InCheck(t, "status", EnumText.AllDbValues<RequestStatus>()));
            e.Property(x => x.Message).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            // One open request per person per card.
            e.HasIndex(x => new { x.CardId, x.UserId }).IsUnique().HasFilter("status = 'requested'");
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Card).WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.Card.DeletedAt == null && x.User.DeletedAt == null);
        });

        modelBuilder.Entity<ActivityPlan>(e =>
        {
            e.ToTable("activity_plans", t =>
            {
                t.HasCheckConstraint("ck_activity_plans_seat_limit", "seat_limit BETWEEN 2 AND 100");
                InCheck(t, "category", EnumText.AllDbValues<PlanCategory>());
                InCheck(t, "destination_precision", EnumText.AllDbValues<LocationPrecision>());
                InCheck(t, "audience", EnumText.AllDbValues<PlanAudience>());
                InCheck(t, "status", EnumText.AllDbValues<PlanStatus>());
            });
            e.Property(x => x.Title).HasMaxLength(80);
            e.Property(x => x.Category).HasMaxLength(16);
            e.Property(x => x.Origin).HasColumnType("geography (point, 4326)");
            e.Property(x => x.OriginAreaLabel).HasMaxLength(120);
            e.Property(x => x.Destination).HasMaxLength(200);
            e.Property(x => x.DestinationPrecision).HasMaxLength(16);
            e.Property(x => x.TimeZoneId).HasMaxLength(64);
            e.Property(x => x.Purpose).HasMaxLength(1000);
            e.Property(x => x.Audience).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.OriginPublic)
                .HasColumnType("geography (point, 4326)")
                .HasComputedColumnSql($"ST_SnapToGrid(origin::geometry, {AccessRules.PublicGridDegrees.ToString(CultureInfo.InvariantCulture)})::geography", stored: true);
            e.HasIndex(x => x.OriginPublic).HasMethod("gist");
            e.HasIndex(x => x.StartsAt);
            e.HasIndex(x => x.CardId);
            e.HasIndex(x => x.HostId);
            e.HasOne(x => x.Card).WithMany(c => c.Plans).HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Host).WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.DeletedAt == null && x.Card.DeletedAt == null);
        });

        modelBuilder.Entity<PlanParticipant>(e =>
        {
            e.ToTable("plan_participants", t => InCheck(t, "status", EnumText.AllDbValues<MembershipStatus>()));
            e.HasKey(x => new { x.PlanId, x.UserId });
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Plan).WithMany(p => p.Participants).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<VacationCard>().WithMany().HasForeignKey(x => x.SourceCardId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => x.Plan.DeletedAt == null && x.User.DeletedAt == null);
        });

        modelBuilder.Entity<PlanRequest>(e =>
        {
            e.ToTable("plan_requests", t => InCheck(t, "status", EnumText.AllDbValues<RequestStatus>()));
            e.Property(x => x.Message).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.PlanId, x.RequesterId }).IsUnique().HasFilter("status = 'requested'");
            e.HasIndex(x => x.RequesterId);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<VacationCard>().WithMany().HasForeignKey(x => x.SourceCardId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.Plan.DeletedAt == null && x.Requester.DeletedAt == null);
        });

        modelBuilder.Entity<Conversation>(e =>
        {
            e.ToTable("conversations", t =>
            {
                InCheck(t, "type", EnumText.AllDbValues<ConversationType>());
                t.HasCheckConstraint("ck_conversations_ref", "(type = 'direct') = (ref_id IS NULL)");
            });
            e.Property(x => x.Type).HasMaxLength(16);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.Type, x.RefId }).IsUnique().HasFilter("ref_id IS NOT NULL");
        });

        modelBuilder.Entity<ConversationMember>(e =>
        {
            e.ToTable("conversation_members");
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.Property(x => x.JoinedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.UserId);
            e.HasOne<Conversation>().WithMany(c => c.Members).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(e =>
        {
            e.ToTable("messages");
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            e.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MeetConfirmation>(e =>
        {
            e.ToTable("meet_confirmations", t => InCheck(t, "answer", EnumText.AllDbValues<MeetAnswer>()));
            e.HasKey(x => new { x.PlanId, x.UserId });
            e.Property(x => x.Answer).HasMaxLength(16);
            e.Property(x => x.AnsweredAt).HasDefaultValueSql("now()");
            e.HasOne<ActivityPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("reviews", t =>
            {
                t.HasCheckConstraint("ck_reviews_stars", "stars BETWEEN 1 AND 5");
                t.HasCheckConstraint("ck_reviews_not_self", "reviewer_id <> reviewee_id");
            });
            e.Property(x => x.Text).HasMaxLength(1000);
            e.Property(x => x.ReplyText).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.PlanId, x.ReviewerId, x.RevieweeId }).IsUnique();
            e.HasIndex(x => x.RevieweeId);
            e.HasOne<ActivityPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewer).WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewee).WithMany().HasForeignKey(x => x.RevieweeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("reports", t =>
            {
                InCheck(t, "target_type", EnumText.AllDbValues<ReportTargetType>());
                InCheck(t, "status", EnumText.AllDbValues<ReportStatus>());
            });
            e.Property(x => x.TargetType).HasMaxLength(16);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.ResolutionNote).HasMaxLength(1000);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Block>(e =>
        {
            e.ToTable("blocks", t => t.HasCheckConstraint("ck_blocks_not_self", "blocker_id <> blocked_id"));
            e.HasKey(x => new { x.BlockerId, x.BlockedId });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.BlockedId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Type).HasMaxLength(48);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PushSubscription>(e =>
        {
            e.ToTable("push_subscriptions");
            e.Property(x => x.Endpoint).HasMaxLength(1000);
            e.Property(x => x.P256dh).HasMaxLength(200);
            e.Property(x => x.Auth).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Endpoint).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Consent>(e =>
        {
            e.ToTable("consents", t => InCheck(t, "kind", EnumText.AllDbValues<ConsentKind>()));
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Version).HasMaxLength(32);
            e.Property(x => x.GrantedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.UserId, x.Kind });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    /// <summary>CHECK that an enum column only holds known values.</summary>
    private static void InCheck<TEntity>(TableBuilder<TEntity> table, string column, IEnumerable<string> values)
        where TEntity : class
    {
        var list = string.Join(", ", values.Select(v => $"'{v}'"));
        table.HasCheckConstraint($"ck_{table.Name}_{column}", $"{column} IN ({list})");
    }
}
