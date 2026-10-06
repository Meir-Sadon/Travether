# Phase → Model → Session Map

Which Claude model should run each step of [PLAN.md](PLAN.md), and whether to **start a new session** or **continue in the same one**. The goal is to save tokens and keep context clean.

> **Rule for every model:** before starting work on any step, find it in the table below and compare it with the model you are running as. If they don't match, **stop and ask the user for confirmation** before doing anything. See [CLAUDE.md](CLAUDE.md).

## Models

| Model | ID | Use for |
|-------|----|---------|
| **Opus 5.5** | `claude-opus-5-5` | Architecture, data model, security/privacy logic (authorization), complex algorithms, research and planning |
| **Sonnet 5.5** | `claude-sonnet-5-5` | Most feature implementation: UI screens, CRUD, integrations, tests |
| **Haiku 4.5** | `claude-haiku-4-5-20251001` | Small, mechanical work: copy/text, translations, config tweaks, renames, docs, simple bug fixes |

## Session legend

- 🆕 **New session:** start a fresh chat. Use it when the step needs different context from what came before, at phase boundaries, or after a long session (old context costs tokens on every message).
- 🔁 **Same session:** continue in the chat that did the previous step. Use it when the step builds directly on code and decisions just made, so re-reading them would cost more.

## Map

| Step | Phase | Task (PLAN.md ref) | Model | Session | Why |
|------|-------|--------------------|-------|---------|-----|
| R0 | Research | Competitive research + PLAN.md (done) | Opus 5.5 | — | Completed |
| R1 | Research *(on hold)* | Starting destinations research (§11) | Opus 5.5 | 🆕 New | Multi-source research and synthesis; unrelated to code context |
| 0.1 | Phase 0 | Repo setup: React/Vite frontend, .NET 10 API, Docker, Render, lint, Vitest, xUnit, GitHub Actions (§6, §7) | Sonnet 5.5 | 🆕 New | Standard scaffolding; fresh start of the build |
| 0.2 | Phase 0 | PostgreSQL + PostGIS, EF Core migrations, **full data model + authorization rules** (§6.1, §4.9) | Opus 5.5 | 🆕 New | Security-critical design that every later step depends on |
| 0.3 | Phase 0 | Design tokens + base components (§5) | Sonnet 5.5 | 🔁 Same as 0.1 | Builds directly on the scaffolded frontend |
| 0.4 | Phase 0 | i18n scaffolding (en) + logical CSS for RTL (§6) | Haiku 4.5 | 🔁 Same as 0.3 | Small mechanical config on top of 0.3 |
| 0.5 | Phase 0 | Clickable mockups of key screens (§5) | Sonnet 5.5 | 🔁 Same as 0.3 | Reuses components and tokens just built |
| 1.1 | Phase 1 | Auth: email/password, OTP, Google/Apple, 18+ gate, onboarding (§4.1) | Sonnet 5.5 | 🆕 New | New phase; auth context only |
| 1.2 | Phase 1 | Profile + gradual completion + contact-verified badge (§4.1) | Sonnet 5.5 | 🔁 Same as 1.1 | Continues the user/onboarding flow |
| 1.3 | Phase 1 | Vacation Cards: CRUD, cities + dates, visibility, share link + QR, public preview (§4.2) | Sonnet 5.5 | 🆕 New | New domain; keep context focused |
| 1.4 | Phase 1 | Card join requests, approve/reject, co-admins, leave/remove (§4.2, §4.5) | Sonnet 5.5 | 🔁 Same as 1.3 | Same entity and code |
| 1.5 | Phase 1 | Activity Plans: templates, seat limit, open/groups-only, **location privacy** (§4.3) | Sonnet 5.5 | 🆕 New | New domain |
| 1.6 | Phase 1 | **Discovery & matching:** PostGIS radius + date overlap, distance rounding, sort, filters (§4.4) | Opus 5.5 | 🆕 New | Geo queries, performance and privacy-sensitive rounding |
| 1.7 | Phase 1 | Plan join requests + status stepper (§4.5) | Sonnet 5.5 | 🔁 Same as 1.5 | Builds on plan code |
| 1.8 | Phase 1 | In-app chat (card + plan) via SignalR; voluntary contact sharing (§4.3, §2) | Sonnet 5.5 | 🆕 New | Separate realtime subsystem |
| 1.9 | Phase 1 | Notifications: in-app, email, Web Push (PWA), daily digest (§4.7) | Sonnet 5.5 | 🆕 New | Separate subsystem (service worker, push, email) |
| 1.10 | Phase 1 | **"Did you meet?" + double-blind reviews + rating rules** (§4.6) | Opus 5.5 | 🆕 New | Subtle state machine + visibility rules enforced in the API |
| 1.11 | Phase 1 | Safety: block, report, moderation admin, rate limits, safety tips (§4.8) | Sonnet 5.5 | 🆕 New | Cross-cutting; fresh context |
| 1.12 | Phase 1 | Privacy: policy pages, consent, data export, delete account (§4.9) | Opus 5.5 | 🔁 Same as 1.11 | Legal/data-deletion correctness; shares safety context |
| 1.13 | Phase 1 | Calendar (.ics) export + WhatsApp share (§4.3) | Haiku 4.5 | 🆕 New | Small, self-contained |
| 1.14 | Phase 1 | Analytics funnels (PostHog) + Sentry (§6, §9) | Haiku 4.5 | 🔁 Same as 1.13 | Config-level integration |
| 1.15 | Phase 1 | E2E tests for core flows (§7) | Sonnet 5.5 | 🆕 New | Needs a broad, fresh read of the app |
| 1.16 | Phase 1 | Pre-launch security & code review | Opus 5.5 | 🆕 New | Independent, unbiased review of the whole MVP |
| 2.1 | Phase 2 | Map view for Discover | Sonnet 5.5 | 🆕 New | UI feature on existing queries |
| 2.2 | Phase 2 | ID + selfie verification via vendor | Opus 5.5 | 🆕 New | Biometric/identity data, vendor security |
| 2.3 | Phase 2 | Women-only plans, trusted-contact sharing, safety check-in | Sonnet 5.5 | 🔁 Same as 2.2 | Depends on verification attributes just added |
| 2.4 | Phase 2 | Interests/language/age filters + better ranking | Opus 5.5 | 🆕 New | Ranking/matching logic |
| 2.5 | Phase 2 | "Book this together" affiliate links | Sonnet 5.5 | 🆕 New | Standalone integration |
| 2.6 | Phase 2 | Waitlists, recurring plans, cost-splitting | Sonnet 5.5 | 🆕 New | Plan-domain features |
| 2.7 | Phase 2 | Hebrew + RTL localization; more languages | Haiku 4.5 | 🆕 New | Mostly translation and string work (Sonnet for layout fixes) |
| 2.8 | Phase 2 | Trip memories (shared photos) | Sonnet 5.5 | 🆕 New | Storage + UI feature |
| 3.1 | Phase 3 | Sponsored activities self-serve portal | Opus 5.5 | 🆕 New | Payments, new user type, architecture |
| 3.2 | Phase 3 | "Plus" subscription | Sonnet 5.5 | 🔁 Same as 3.1 | Reuses payments setup from 3.1 |
| 3.3 | Phase 3 | AI suggestions | Opus 5.5 | 🆕 New | LLM integration design |
| 3.4 | Phase 3 | Native apps (React Native / Expo) | Opus 5.5 | 🆕 New | Platform architecture decision |
| — | Any | Small bug fix, typo, copy change, dependency bump | Haiku 4.5 | 🔁 Same | Cheapest model; no new context needed |

## General session hygiene

- Start a **new session at every phase boundary** (0 → 1 → 2 → 3).
- When a session gets long (many files read, many turns), start a new one even if the table says 🔁. Commit first and mention the step ID in the first message.
- First message of a new session: *"Working on step X.Y from PHASE_MODEL_MAP.md"*. The model will verify the match.
- Mark completed steps in [PLAN.md](PLAN.md) checklists so the next session knows the state.
