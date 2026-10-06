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
| Leave the card | `Member` or `CoAdmin` (the owner hands over ownership or deletes the card instead) |
| Appoint co-admins, hand over ownership, remove members, edit or delete the card, upload the cover, issue a new share link | `Owner` |

**Card join requests** are refused with `AlreadyMember` (already active), `AlreadyRequested` (an open request exists) or `TripEnded` (the end date has passed); invite-only cards need the share slug in the request. Approving a request whose requester was banned or deleted, or who is now blocked either way with the owner, marks it `expired` and answers `RequestExpired`. Two people deciding at once: the second gets `AlreadyDecided`. Handing over ownership makes the old owner a co-admin. Leaving or being removed also ends the person's participation in this card's plans that they joined as its member.

## Activity Plans (§4.3, §4.5)

Levels: `None` → `Public` → `Participant` → `Host`. A plan is `Public` to everyone except people who blocked the host or were blocked by them (`None`). Plans are discoverable even when their card is invite-only; the card itself stays private.

| What | Public | Participant / Host |
|------|:---:|:---:|
| Title, category, date/time, seats left, host's public profile | ✔ | ✔ |
| Meeting area label and **rounded distance** (to the grid-snapped point, see [DATA_MODEL.md](DATA_MODEL.md#meeting-point-privacy-origin_public)) | ✔ | ✔ |
| **Exact meeting point** (name and coordinates) | | ✔ |
| Destination, when marked *Exact* | | ✔ |
| Destination, when marked *Regional* | ✔ | ✔ |
| Plan chat | | ✔ |

Members of the plan's own card who haven't joined see it like the public, plus the list of who's going; they self-join instead of requesting.

**Plan actions** (`PlansController`):

| Action | Needs |
|--------|-------|
| Create a plan, list the card's plans | `Member` of the card. The host may add card members as participants. |
| Take a free seat (self-join) | `Member` of the plan's card, plan `open`, seats free, start in the future. Two people can't take the last seat (the plan row is locked). |
| Leave | `Participant`. The host cancels instead (`HostCannotLeave`). |
| Edit, cancel | The host, or a co-admin/owner of the plan's card. Cancelling expires open requests. |

**Discover** (`GET /api/discover`, open to visitors) lists open plans with free seats whose public point is within the radius (default 30 km, max 100) of the searcher's origin and whose local date falls within the searcher's dates. It leaves out plans of the searcher's own trips, plans they already joined, and hosts they blocked or who blocked them. Distances are rounded from the grid-snapped point, like the plan page.

A plan's date must fall within its card's dates (`PlanOutsideTrip`) and in the future (`PlanInPast`). Times are entered in the meeting point's local time; the time zone comes from the coordinates.

**Deciding requests:** the host, or a co-admin/owner of the plan's card.

**Requesting to join** (`POST /api/plans/{id}/requests`) is refused when (the `JoinDenial` rule, then the API code):

| Rule | API code | Condition |
|------|----------|-----------|
| `Blocked` | 404 `NotFound` | Either side blocked the other |
| `AlreadyParticipant` | `AlreadyParticipant` | Already an active participant |
| `OwnCardMembersSelfJoin` | `JoinDirectly` | Member of the plan's own card (they self-join) |
| `NotOpen` / `Full` | `PlanNotOpen` / `PlanFull` | Plan is full, cancelled, done or already started |
| `AlreadyRequested` | `AlreadyRequested` | An open request exists |
| `GroupsOnlyNeedsCard` | `GroupsOnly` | "Groups only" plan and no source card given |
| `SourceCardNotYours` | `SourceCardNotYours` | Source card given but requester isn't an active member of it |
| `PartyNotInSourceCard` | `PartyNotInSourceCard` | Someone in the party isn't an active member of the source card |
| | `NotEnoughSeats` | Requester plus party don't fit in the free seats |
| | `PartyAlreadyGoing` | Someone in the party is already a participant |

**Deciding** (host, or the card's owner/co-admins): approval locks the plan row, re-checks seats (`NotEnoughSeats` keeps the request open), and seats the requester and their party with the source card recorded. If anyone coming was banned, deleted or is now blocked either way with the host, or the party left the source card, the request becomes `expired` (`RequestExpired`). When approval fills the plan, the other open requests expire. Requests also expire when the plan is cancelled or starts (a background sweep every 5 minutes). The host or the card's admins can remove a participant (status `removed`); the host can't be removed.

## Chat

| Conversation | Who can read and post |
|--------------|-----------------------|
| Card | Active card members |
| Plan | Active plan participants and the host |
| Direct | Its two members, unless either blocked the other |

The REST API (`/api/chats`) does every read and write and checks access each time; the SignalR hub (`/hubs/chat`) only pushes new messages to members who are signed in, minus anyone who blocked the sender. Sharing a phone or WhatsApp number posts the sender's own profile number as a message (`PhoneRequired` when they have none); nobody else's number is ever revealed. Plan chats stay listed for 30 days after the plan.

## Ratings (§4.6)

- **"Did you meet?"** Only active participants answer, once each (`AlreadyAnswered`), from the plan's start (`PlanNotOver`) until 14 days after it (`AnswerClosed`). Answers: *Yes, we met* / *It was cancelled* / *I didn't go*. Nobody sees another person's answer: until both said yes, the other side only shows as *waiting*. From 10:00 local time the day after, the plan becomes `done` and everyone on it with company is asked (again on day 7 if they haven't answered).
- Reviewing opens only when **both** people answered *Yes, we met*, and closes **14 days after the second answer** (`ReviewNotOpen`). People who left, were removed, are banned or deleted, or are blocked either way can't be reviewed.
- One review per reviewer, reviewee and plan (`AlreadyReviewed`); 1–5 stars (`InvalidStars`); never yourself (also enforced by the database).
- **Double-blind:** a review is visible to anyone other than its author only once the other person's review exists or the window has closed. This is computed from the data, so it holds even if the job that sets `published_at` is late. The person reviewed learns *that* someone reviewed them, not what they wrote.
- Published reviews about someone are visible to whoever can see their profile, minus reviewers blocked either way with the viewer. A deleted or banned reviewer shows as "Deleted user".
- The person reviewed may **reply once**, publicly, after the review is visible (`ReviewNotPublished`, `AlreadyReplied`).
- The average shows only from **3** published reviews; the count is always shown.
- Reporting an abusive review comes with step 1.11.
