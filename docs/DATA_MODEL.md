# Data model and migrations

PostgreSQL 17 + PostGIS 3.5, mapped with EF Core 10 and Npgsql. Code: [`backend/src/Travether.Api/Domain`](../backend/src/Travether.Api/Domain) (entities) and [`Data/TravetherDbContext.cs`](../backend/src/Travether.Api/Data/TravetherDbContext.cs) (mapping). Who may read what is in [AUTHORIZATION.md](AUTHORIZATION.md).

## Conventions

- Tables and columns are `snake_case`; primary keys are `uuid`.
- Enums are stored as readable `snake_case` text (`co_admin`, `groups_only`) with a `CHECK` constraint listing the allowed values.
- Timestamps are `timestamptz`, always written in UTC. Calendar dates (card dates, date of birth) are `date`.
- Locations are `geography(Point, 4326)` (longitude, latitude).
- Soft delete: `users`, `vacation_cards` and `activity_plans` have `deleted_at`. Cards, plans and the rows that hang off them are hidden by EF Core global query filters. Users have no global filter, so messages and reviews by a deleted account still load (shown as "Deleted user"); access checks treat deleted and banned users as absent.

## Tables

The tables follow [PLAN.md §6.1](../PLAN.md#61-data-model-initial). Additions and decisions made while building it:

| Table | Change from §6.1 | Why |
|-------|------------------|-----|
| `users` | `verification_level` became `verification_badges` (bit set: contact 1, photo 2, ID 4). Added `role` (`traveler`/`moderator`) and `banned_at`. | Badges are independent of each other; moderators are users (§3); bans must stop access everywhere. |
| `users` | Unique email among non-deleted accounts, stored lower-case (CHECK). | A deleted account frees its email for a new sign-up. |
| `users` | Added `password_hash` (nullable) and `session_version` (step 1.1). | Code-only and Google/Apple accounts have no password; bumping the version ends every session ([AUTH.md](AUTH.md)). |
| `external_logins` | New (step 1.1): `provider` (`google`/`apple`), `subject`, `user_id`. PK (provider, subject). | Google/Apple accounts linked to a user. |
| `login_codes` | New (step 1.1): `email`, `purpose`, `code_hash`, `attempts`, `expires_at`, `consumed_at`. | Emailed one-time codes; only an HMAC is stored. |
| `vacation_cards` | CHECK `ends_on >= starts_on`, at least one region. GiST index on `daterange(starts_on, ends_on, '[]')`. | Date-overlap matching (§4.4). The expression index is raw SQL in the migration. |
| `card_members` | Partial unique index: one `owner` per card. | |
| `card_requests`, `plan_requests` | Partial unique index: one open (`requested`) request per person per card/plan. | Stops double-submits; a rejected request doesn't block a new one. |
| `activity_plans` | Added `time_zone_id` (IANA), `origin_public` and `origin_name` (the meeting point's name, private like `origin`). `seat_limit` 2–100. | Local dates for reminders and "Did you meet?" (§4.6). `origin_public` is explained below. |
| `conversation_members` | New. Members of `direct` conversations. | Card and plan chats derive members from memberships; direct chats need their own list. CHECK: `ref_id` is null exactly for `direct`. |
| `messages` | Added `kind` (`text`, `contact_phone`, `contact_whatsapp`; step 1.8). | A shared number is a message of its own kind, so the client can render it as a call / WhatsApp link. |
| `conversation_reads` | New (step 1.8): `conversation_id`, `user_id`, `last_read_at`. PK (conversation, user). | Unread counts for the inbox and the tab badge. |
| `reviews` | Added `replied_at`. Unique per (plan, reviewer, reviewee); stars 1–5; no self-review. | |
| `reports` | Added `resolved_by_id`, `resolved_at`, `resolution_note`. | EU DSA: reasons are given when content is removed (§4.9). |
| `push_subscriptions` | Added `id`; `keys` split into `p256dh` and `auth`. | Web Push needs both keys; endpoints are unique. |
| `consents` | New: `user_id`, `kind`, `version`, `granted_at`, `withdrawn_at`. | Consent records for GDPR / Israeli PPL Amendment 13 (§4.9). |

### Meeting-point privacy: `origin_public`

`activity_plans.origin` is the exact meeting point and only approved participants ever receive it. `origin_public` is a stored generated column, `ST_SnapToGrid(origin::geometry, 0.01)::geography`: the point snapped to a grid of about 1 km. Public distances and discovery radius queries use `origin_public` (GiST-indexed), and the result is rounded again (`<1 km`, whole km to 10 km, then 5 km steps). Measuring from the snapped point means that moving your own origin around and reading the rounded distances can't pin down the exact meeting point.

## Migrations workflow

The EF Core CLI is pinned in [`dotnet-tools.json`](../dotnet-tools.json):

```bash
dotnet tool restore                     # once
docker compose up -d db                 # local PostGIS on :5432 (user/password/db: travether)

cd backend
# after changing an entity or TravetherDbContext:
dotnet ef migrations add <PascalCaseName> -p src/Travether.Api -s src/Travether.Api -o Data/Migrations
dotnet ef database update -p src/Travether.Api -s src/Travether.Api
```

- Review the generated migration before committing. Anything EF Core can't model (expression indexes, extensions with options) goes in as `migrationBuilder.Sql(...)` with a comment.
- The test `SchemaTests.Model_has_no_pending_changes` fails if the model changed without a migration.
- Production: Render sets `Database__MigrateOnStartup=true`, so the API applies pending migrations when it starts. Migrations must therefore be backwards compatible with the version still running during a deploy (add columns as nullable first, backfill, then tighten).
- Integration tests start a throwaway PostGIS container with Testcontainers and apply all migrations, so Docker must be running for `dotnet test`.
