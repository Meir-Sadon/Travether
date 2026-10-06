# Travether — Travel Together · Project Plan

> Status: approved direction, pre-development · Last updated: 2026-10-06
> Model & session per step: [PHASE_MODEL_MAP.md](PHASE_MODEL_MAP.md)
> Inputs: original Hebrew spec (אפיון פרויקט Travether), [competitive research report](reports/Travether%20competitive%20research.md), and the founder's decisions recorded below.

---

## 1. Product vision

Travether connects travelers around the world, solo or in groups, who are in the **same place at the same time**, so they can do things together, split costs and meet people.

The core idea, which no competitor combines today:
1. Travelers organise themselves in **Vacation Cards**: a trip group with a destination and dates.
2. From a Vacation Card, members publish **Activity Plans**: what, where and when.
3. Other travelers and groups in the same city on the same dates **discover those plans**, sorted by distance, and **request to join**.
4. After meeting, people **rate each other**, which builds a trust layer over time.

**Positioning:** "Find people to do things with on your trip", not a dating app and not a generic buddy finder. The unit is a concrete activity at a concrete time.

---

## 2. Decisions log

| # | Topic | Decision |
|---|-------|----------|
| 1 | Market | **Global from day one** (English UI; i18n-ready, with Hebrew and RTL added later). Marketing seeds 2–3 starting destinations, **to be chosen after a separate research task** (see §11). |
| 2 | Identity | **No national ID number.** Sign up and log in with email + password, email or phone one-time code, and Google/Apple sign-in. Optional identity verification through a vendor (we store only the result, never the document). |
| 3 | Contact | **In-app chat** replaces the automatic phone/email reveal. Each user may *choose* to share their own phone or WhatsApp inside the chat. |
| 4 | Matching | Match on **overlapping dates and city/region radius**, not country alone. |
| 5 | Joining | **Anyone can request to join** another Vacation Card or Activity Plan, solo travelers included. Plans have a seat limit; the owner or co-admins approve. |
| 6 | Ratings | Prompt both sides with **"Did you meet?"** after the activity date. If both confirm, there is a **14-day window**; reviews stay hidden until both are submitted or the window closes. Ratings go to **individuals**. A profile shows an average only after **3+ reviews**. |
| 7 | Visual style | **"Fresh Explorer"**: Airbnb-like structure (white background, photo-led cards, rounded corners, list + map toggle) with a modern palette: deep ink green + signature lime instead of coral red (changed after the prototype review). |
| 8 | Platform | **Mobile-first web app (PWA)**, with a guided "Add to Home Screen" step for push notifications. Native apps later. |
| 9 | Monetization | **Free core forever.** Revenue order: (1) booking affiliate links → (2) sponsored activities → (3) optional "Plus" subscription. Never charge for contact, matching or safety. |

---

## 3. Users and roles

| Role | Description |
|------|-------------|
| **Visitor** | Not signed in. Can open a shared Vacation Card or Activity Plan link or QR code and see a public preview. Asked to sign up only on "Request to join". |
| **Traveler** | Registered user. Creates or joins Vacation Cards, requests to join plans, chats, rates. |
| **Card owner** | Creator of a Vacation Card. Approves or rejects joiners, appoints co-admins, deletes the card. |
| **Co-admin** | A card member promoted by the owner. Can approve joiners and act for the group on plans. |
| **Plan host** | Creator of an Activity Plan (can share hosting with co-admins). Approves join requests and closes the plan. |
| **Moderator** (internal) | Reviews reports, hides content, bans users. |

---

## 4. Core entities (functional spec)

### 4.1 User profile
| Field | Required | Visibility |
|-------|----------|------------|
| Display name (first name) | ✔ | Public |
| Full name | ✔ | Private (shown only to approved co-participants) |
| Email | ✔ | Private |
| Phone | Optional (required for SMS code) | Private; the user can share it in chat |
| Date of birth | ✔ (18+ only) | Private; **age** shown publicly |
| Country of origin | ✔ (list) | Public (flag) |
| Photo | Optional (encouraged) | Public |
| Bio | Optional | Public |
| Languages spoken | Optional | Public |
| Interests / travel style tags | Optional (e.g., hiking, nightlife, food, budget, culture) | Public |
| Verification badges | Earned | Public: ✉️ contact-verified · 📸 photo-verified · 🪪 ID-verified |
| Rating average + count | Earned (3+ reviews) | Public |

The profile starts minimal and is filled in gradually, with a profile-strength prompt. The onboarding wizard stays under about 60 seconds.

### 4.2 Vacation Card (trip group)
- Name ✔ · Destination country ✔ · **City/region** (✔, one or more) · From/To dates (**✔**, needed for date matching) · Description (optional) · Cover photo (optional).
- Visibility: **Public** (discoverable, anyone can request) or **Invite-only** (link or QR only).
- **Share link + QR code**, styled like a boarding pass. The preview works without an account.
- Join requests: owner and co-admins approve or reject. Requesters can add a short message.
- Owner can appoint co-admins, remove members and delete the card. Members can leave.
- Group chat for card members.

### 4.3 Activity Plan
- Created from within a Vacation Card.
- Fields: Title ✔ · Category ✔ (template: Hike, Day trip, Food, Nightlife, Tour, Beach, Transport/ride share, Other) · Meeting area/origin ✔ · Destination ✔ · Exact/Regional flag ✔ · Date ✔ · Time ✔ · Purpose/description · **Seat limit** ✔ · **Audience:** "Open to everyone" (default) or "Groups only".
- Host picks participants from the card's members; other card members can self-join.
- Outsiders, whether solo travelers or members of other groups, **request to join**, as individuals or bringing members of their own card along. The host or co-admins approve.
- **Location privacy:** the public view shows the area and a rounded distance (e.g., "~2 km"). The exact meeting point is visible only to approved participants.
- Plan chat opens for approved participants.
- Extras: add to calendar (.ics), share to WhatsApp, **"Book this together"** affiliate link when relevant.

### 4.4 Discovery and matching
A plan is shown to a user if:
- it is in the same **city/region** (within a configurable radius, default 30 km), **and**
- the plan date falls within the user's Vacation Card dates (**date overlap**), **and**
- the plan is open and has free seats.

Sorting: distance from the user's chosen origin (default), then date and rating. Filters: category, date, group size, language, and age range (phase 2). The view is a list by default with a map toggle.

### 4.5 Join request lifecycle
```
Requested ──► Approved ──► (chat unlocked, exact location revealed)
    │             └──► Left / Removed
    ├──► Rejected
    ├──► Withdrawn
    └──► Expired (plan date passed / plan full / plan deleted)
```
The requester sees a clear step indicator: *Requested → Approved → Chat open*.

### 4.6 Closing a plan and ratings
1. The day after the plan date, every approved participant gets: **"Did this happen?"** with the options *Yes, we met* / *Cancelled* / *I didn't go*.
2. Where two people both confirmed *Yes*, each may rate the other: 1–5★ plus optional text, within **14 days**.
3. Reviews stay **hidden until both sides submit** or the window closes.
4. Ratings attach to the **person reviewed**. A profile shows the average only once it has **≥3 reviews**, and always shows the count.
5. Users can reply publicly once to a review and report abusive reviews.
6. The host can still delete a plan at any time. Deleting before the date notifies participants and marks the plan *Cancelled*.

### 4.7 Notifications
| Event | Channel | Timing |
|-------|---------|--------|
| Join request received / approved / rejected | Push + in-app + email | Real-time |
| New chat message | Push + in-app | Real-time (batched per conversation) |
| New matching plan nearby | In-app + **daily digest** (push/email) | Digest |
| Plan reminder | Push | 24 h and 2 h before |
| "Did you meet?" / review reminder | Push + email | Next day, then day 7 |

Users control each category. Quiet hours follow the user's local timezone.

### 4.8 Trust and safety
- Must be 18+. Accept terms and community guidelines at sign-up.
- **Block and report** on profiles, plans, cards and chat messages, with a moderation queue and an admin panel.
- Rate limits on join requests, messages and new accounts. Stop banned users from simply re-registering (device and email/phone checks).
- Safety tips shown before a first meetup ("meet in a public place").
- **Share my plan with a trusted contact:** a link to the plan's time and place.
- Optional **women-only** plans (phase 2, requires a verified gender attribute).
- Verification vendor (e.g., Stripe Identity, about $1.50 per check) for the 🪪 badge (phase 2). Photo verification via selfie in phase 2.

### 4.9 Privacy and compliance
- Collect as little as possible; no national ID numbers. Precise locations are stored only for meeting points and shown only to approved participants. Public distances are rounded.
- GDPR and Israeli Privacy Protection Law (Amendment 13): privacy policy, consent records, data export and **account deletion** in self-service, data retention policy, DPIA before launch.
- EU DSA basics: a channel for reporting illegal content and reasons given when content is removed.
- **Before public launch:** review by privacy counsel, and a check of the "Travel Together" name and trademark (Polarsteps uses the phrase).

---

## 5. Design direction — "Fresh Explorer"

| Token | Value (initial, contrast-check before use) |
|-------|--------------------------------------------|
| Background | `#FFFFFF` / surface `#F7F7F7` |
| Text | `#222222` primary · `#6A6A6A` secondary · brand ink `#0E2F2C` for headlines, active states, own chat bubbles |
| Accent (primary actions, + button, highlights) | Signature lime `#C6F36B`, **always with ink `#0E2F2C` text/icons** (11:1 contrast; never lime text on white) |
| Alerts | `#E5484D` for notification dots only |
| Success / Info / Warning | `#2E8B57` · `#1E6FD9` · `#E8A317` |
| Font | **Rubik** (Google Fonts; covers Latin + Hebrew for later RTL) at 400/500/700 |
| Radius | 12–16 px cards, full-pill buttons and chips |
| Imagery | Large destination photos on Vacation Cards; avatars stacked on plan cards |
| Dark mode | Supported from MVP via tokens |

Key screens:
1. **Landing / public preview**
2. **Sign up / onboarding** (3 steps)
3. **Home:** My trips (Vacation Cards) + "Happening near you"
4. **Vacation Card page:** cover, members, plans, chat, share/QR
5. **Discover:** list ⇄ map, filters
6. **Activity Plan page:** details, participants, join button, status steps
7. **Inbox:** chats and requests
8. **Profile:** badges, rating, reviews
9. **Create Card / Create Plan** forms (bottom-sheet style on mobile)
10. **Settings:** notifications, privacy, delete account

Interaction principles: one primary action per screen; thumb-reachable bottom navigation (Home · Discover · + Create · Inbox · Profile); no swiping; clear empty states that invite creating the first plan; WCAG AA contrast.

---

## 6. Technical architecture (same stack as kuskus-shel-ima)

Same technologies and conventions as the [kuskus-shel-ima](https://github.com/Meir-Sadon/kuskus-shel-ima) repo, so tooling, deployment and know-how carry over.

| Layer | Choice | Why |
|-------|--------|-----|
| Frontend | **React 19 + Vite + TypeScript** (`frontend/`), React Router, plain CSS with design tokens | Same as kuskus; fast mobile-first SPA |
| i18n | **i18next / react-i18next**, `<html lang dir>` kept in sync, logical CSS properties | English at launch; Hebrew/RTL = one more JSON file |
| PWA | Web App Manifest + service worker (`vite-plugin-pwa`); Web Push (VAPID) | Push on Android, and on iOS after "Add to Home Screen" |
| Backend | **ASP.NET Core (.NET 10) Web API** (`backend/`), controllers + DTO validation, errors as codes | Same as kuskus |
| Database | **PostgreSQL** via **EF Core + Npgsql**, with **PostGIS** (`Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`) | Radius + date-overlap queries; migrations in EF Core |
| Auth | JWT in **httpOnly cookies**, CSRF header guard; email one-time code + **Google / Apple** sign-in | Same cookie pattern as kuskus; no national ID |
| Authorization | Enforced **server-side** in the API (participant checks on every query/endpoint) | Private fields, exact meeting points and chats only for approved participants |
| Realtime chat | **ASP.NET Core SignalR** | Built into .NET; card and plan chat rooms |
| Images | **Cloudinary** behind an `IImageStore` interface (fallback when not configured) | Same as kuskus |
| Email | Resend or Postmark behind an interface (simulated sender locally) | Transactional + digest |
| Maps & places | Mapbox or Google Places Autocomplete | City/region selection, meeting points |
| QR | `qrcode` library on the client | Share cards |
| Analytics / errors | PostHog (EU) + Sentry | Funnels: signup → card → plan → request → meetup |
| Local dev | **Docker / docker compose** (Postgres+PostGIS, API, frontend behind nginx) | `docker compose up --build` |
| Hosting | **Render** (one Docker web service: API serves the built frontend) + **Neon** Postgres (EU) | Same as kuskus; one origin, simple deploys |
| Testing | Frontend: **Vitest + Testing Library** (jsdom), **oxlint**; Backend: **xUnit** integration tests with **Testcontainers** (Postgres); Playwright E2E later | Same as kuskus |
| CI | GitHub Actions: lint, typecheck/build, frontend + backend tests | |

### 6.1 Data model (initial)
```
users            (id, display_name, full_name, email, phone, dob, country_code, photo_url,
                  bio, languages[], interests[], verification_level, created_at, deleted_at)
vacation_cards   (id, owner_id, name, country_code, regions[], starts_on, ends_on,
                  description, cover_url, visibility, share_slug, created_at, deleted_at)
card_members     (card_id, user_id, role[owner|co_admin|member], status, joined_at)
card_requests    (id, card_id, user_id, message, status, created_at, decided_by, decided_at)
activity_plans   (id, card_id, host_id, title, category, origin geography(point),
                  origin_area_label, destination, destination_precision[exact|regional],
                  starts_at, purpose, seat_limit, audience[open|groups_only],
                  status[open|full|cancelled|done], created_at, deleted_at)
plan_participants(plan_id, user_id, source_card_id, status, joined_at)
plan_requests    (id, plan_id, requester_id, source_card_id, party_user_ids[], message,
                  status, created_at, decided_by, decided_at)
conversations    (id, type[card|plan|direct], ref_id)
messages         (id, conversation_id, sender_id, body, created_at, hidden_at)
meet_confirmations(plan_id, user_id, answer[met|cancelled|no_show], answered_at)
reviews          (id, plan_id, reviewer_id, reviewee_id, stars, text, created_at,
                  published_at, reply_text)
reports          (id, reporter_id, target_type, target_id, reason, status, created_at)
blocks           (blocker_id, blocked_id)
notifications    (id, user_id, type, payload, read_at, created_at)
push_subscriptions(user_id, endpoint, keys, created_at)
```
Implemented with additions; see [docs/DATA_MODEL.md](docs/DATA_MODEL.md) and [docs/AUTHORIZATION.md](docs/AUTHORIZATION.md).
Key queries: `ST_DWithin(origin_public, :point, :radius)` and `daterange(starts_on, ends_on) && :range`, with GiST indexes on both.

---

## 7. Roadmap

### Phase 0 — Foundations (≈1–2 weeks)
- [x] Repo setup: `frontend/` (React + Vite + TS, oxlint, Vitest), `backend/` (.NET 10 Web API + xUnit), Dockerfile, docker-compose, render.yaml, GitHub Actions
- [x] PostgreSQL + PostGIS (docker compose locally, Neon EU in production), EF Core migrations workflow
- [x] Design tokens, base components (Button, Card, Chip, Avatar stack, BottomSheet, Stepper)
- [x] i18n scaffolding (en), logical CSS for future RTL
- [x] Clickable mockups of key screens ([docs/MOCKUPS.md](docs/MOCKUPS.md))
- [ ] Quick test of the mockups with 5–10 travelers (script in [docs/MOCKUPS.md](docs/MOCKUPS.md#quick-test-with-510-travelers))

### Phase 1 — MVP (≈6–8 weeks)
- [x] Auth: email + password, email OTP, Google and Apple sign-in; 18+ gate; onboarding wizard
- [x] Profile (minimal + gradual completion), contact-verified badge
- [x] Vacation Cards: create/edit/delete, cities + dates, visibility, share link + QR, public preview
- [x] Card join requests (anyone), approve/reject, co-admins, leave/remove
- [x] Activity Plans: templates, seat limit, open/groups-only, location privacy
- [x] Discover: city radius + date overlap, distance sort, list view, basic filters
- [x] Plan join requests with status stepper
- [x] In-app chat (card + plan) via SignalR; voluntary contact sharing
- [x] Notifications: in-app + email + Web Push; daily digest for matches
- [x] "Did you meet?" flow + double-blind reviews + rating display rules
- [ ] Safety: block, report, moderation admin page, rate limits, safety tips
- [ ] Privacy: policy pages, consent, data export, delete account
- [ ] Calendar (.ics) export, WhatsApp share
- [ ] Analytics funnels + Sentry
- [ ] E2E tests for core flows

### Phase 2 — Trust and engagement (after launch)
- [ ] Map view for Discover
- [ ] ID verification (vendor) + selfie photo verification badges
- [ ] Women-only plans, trusted-contact sharing, safety check-in
- [ ] Interests/language/age filters and better match ranking
- [ ] **"Book this together"** affiliate links (GetYourGuide/Viator)
- [ ] Waitlists, recurring plans, cost-splitting for a plan
- [ ] Hebrew + RTL localization; more languages
- [ ] Trip memories (shared photos after a plan)

### Phase 3 — Scale and revenue
- [ ] Sponsored activities from hostels and operators (self-serve portal)
- [ ] "Plus" subscription (featured plans, advanced filters, extra active plans)
- [ ] AI suggestions (plan ideas, compatibility hints)
- [ ] Native apps (React Native / Expo) if PWA limits hurt retention

---

## 8. Go-to-market (launch phase)
1. **Choose 2–3 starting destinations** (research task, see §11).
2. Before launch, prepare 20–30 real plans in each destination: friends who are travelling, hostel partners, community volunteers.
3. Channels: traveler Facebook/WhatsApp groups, Reddit (r/solotravel, r/backpacking), hostel QR posters, local tour operators, Product Hunt.
4. Built-in growth loop: every card and plan has a shareable public preview and QR code, and invitees join in two taps.

## 9. Success metrics
- **North star:** confirmed meetups per week (both sides answered "we met").
- Funnel: visit → sign-up → Vacation Card created → plan viewed → join request → approved → meetup confirmed.
- Liquidity per destination: share of new plans that receive ≥1 join request within 48 h (target ≥40%).
- Trust: reports per 1,000 meetups; average rating; share of users with a verified badge.
- Retention: users who create or join a second plan on the same trip.

## 10. Risks and mitigations
| Risk | Mitigation |
|------|-----------|
| Cold start (empty destinations) | Concentrate on a few destinations, seed plans, public previews, open plans for solo travelers |
| Safety incident | Chat-first, block/report, moderation, verification badges, safety tips, trusted-contact sharing |
| Perceived as a dating app | Activity-first language and UI, group-based plans, reporting of unwanted advances |
| Fake or scam profiles | Rate limits, contact verification, ID badge, reports |
| Hostelworld or a big player copies the idea | Destination density, reputation data and an activity-first experience |
| Legal/privacy exposure | Minimal data, no national ID, counsel review, DPIA, self-service deletion |
| iOS push limits on web | Guided "Add to Home Screen", email fallback, native app in phase 3 |

## 11. Open items
- [ ] **Starting destinations research:** popular places by traveler volume, share of solo travelers, season, existing communities. Decide 2–3.
- [ ] Final brand assets: logo, exact accent color after contrast check.
- [ ] Map provider choice (Mapbox vs Google) based on pricing.
- [ ] Legal: privacy counsel, terms of service, name and trademark check.
- [ ] Moderation staffing for launch (founder + volunteers?).
