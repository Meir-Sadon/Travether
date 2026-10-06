# Authorization rules

Every access decision is made **in the API**, never in the client (PLAN.md §6, §4.9). The rules live in two places:

- [`AccessRules`](../backend/src/Travether.Api/Authorization/AccessRules.cs): pure functions over facts. Unit-tested without a database.
- [`AccessQueries`](../backend/src/Travether.Api/Authorization/AccessQueries.cs): loads those facts from the database (membership, blocks, bans) and returns the access level. Integration-tested against PostGIS.

Phase 1 endpoints must call these instead of writing their own checks. When a rule changes, change it here, in the code, and in the tests together.

## General rules

1. **Signed-out visitors** get public previews only.
2. **Banned or deleted** users count as absent: as viewers they get visitor access at most, and as targets they are not shown.
3. **Blocks work both ways.** If either person blocked the other, they don't see each other's profiles, can't request to join each other's cards or plans, and direct chat between them closes. In group chats, messages from someone you blocked are left out for you.
4. **Membership means active.** Someone who left or was removed loses access immediately, including to chat history.
5. Moderator-hidden messages are not returned to participants.

## People (§4.1)

| Field | Visitor / public | Co-participant¹ | Moderator | Self |
|-------|:---:|:---:|:---:|:---:|
| Display name, age, country, photo, bio, languages, interests, badges, rating | ✔ | ✔ | ✔ | ✔ |
| Full name | | ✔ | ✔ | ✔ |
| Email | | | ✔ | ✔ |
| Phone, date of birth | | | | ✔ |

¹ Shares an **active** Vacation Card membership or an **active** participation in the same Activity Plan. A phone number reaches others only when its owner shares it in chat.

## Vacation Cards (§4.2)

Access levels, lowest to highest: `None` → `Preview` → `Member` → `CoAdmin` → `Owner`.

| Who | Level |
|-----|-------|
| Active member | Their role (`Member`, `CoAdmin`, `Owner`) |
| Anyone not blocked by the owner, **public** card | `Preview` |
| Anyone not blocked by the owner, **invite-only** card opened via its share link | `Preview` |
| Everyone else | `None` |

| Action | Needs |
|--------|-------|
| See preview (name, destination, dates, description, cover, member count) | `Preview` |
| Request to join | exactly `Preview` (not already a member) |
| See members, plans list, card chat | `Member` |
| Approve or reject join requests | `CoAdmin` |
| Appoint co-admins, remove members, edit or delete the card, upload the cover, issue a new share link | `Owner` |

## Activity Plans (§4.3, §4.5)

Levels: `None` → `Public` → `Participant` → `Host`. A plan is `Public` to everyone except people who blocked the host or were blocked by them (`None`). Plans are discoverable even when their card is invite-only; the card itself stays private.

| What | Public | Participant / Host |
|------|:---:|:---:|
| Title, category, date/time, seats left, host's public profile | ✔ | ✔ |
| Meeting area label and **rounded distance** (to the grid-snapped point, see [DATA_MODEL.md](DATA_MODEL.md#meeting-point-privacy-origin_public)) | ✔ | ✔ |
| **Exact meeting point** | | ✔ |
| Destination, when marked *Exact* | | ✔ |
| Destination, when marked *Regional* | ✔ | ✔ |
| Plan chat | | ✔ |

Members of the plan's own card who haven't joined see it like the public; they self-join instead of requesting.

**Deciding requests:** the host, or a co-admin/owner of the plan's card.

**Requesting to join** is refused, with one of these error codes, when:

| Code | Condition |
|------|-----------|
| `Blocked` | Either side blocked the other |
| `AlreadyParticipant` | Already an active participant |
| `OwnCardMembersSelfJoin` | Member of the plan's own card (they self-join) |
| `NotOpen` | Plan is full, cancelled or done |
| `Full` | Active participants ≥ seat limit |
| `AlreadyRequested` | An open request exists |
| `GroupsOnlyNeedsCard` | "Groups only" plan and no source card given |
| `SourceCardNotYours` | Source card given but requester isn't an active member of it |
| `PartyNotInSourceCard` | Someone in the party isn't an active member of the source card |

## Chat

| Conversation | Who can read and post |
|--------------|-----------------------|
| Card | Active card members |
| Plan | Active plan participants and the host |
| Direct | Its two members, unless either blocked the other |

## Ratings (§4.6)

- Reviewing opens only when **both** people answered *Yes, we met*, and closes **14 days after the second answer**.
- One review per reviewer, reviewee and plan; 1–5 stars; never yourself (also enforced by the database).
- **Double-blind:** a review is visible to anyone other than its author only once the other person's review exists or the window has closed. This is computed from the data, so it holds even if the job that sets `published_at` is late.
- The average shows only from **3** published reviews; the count is always shown.
