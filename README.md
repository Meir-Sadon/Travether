# Travether — Travel Together

Find people to do things with on your trip. Travether connects travelers, solo or in groups, who are **in the same place at the same time**: hikes, day trips, dinners, nightlife, or splitting a ride.

- **Vacation Cards:** a trip group with a destination, cities and dates. Share it with a link or QR code; you approve who joins.
- **Activity Plans:** what, where and when, published from a card. Others nearby on the same dates discover it (sorted by distance) and request a seat.
- **In-app chat** once a request is approved. Phone numbers are shared only if a person chooses to.
- **"Did you meet?"** After the activity, people who met rate each other. Reviews stay hidden until both sides submit them.

> Status: **planning and prototype**. No application code yet. The build starts with Phase 0 in [PLAN.md](PLAN.md).

## Documents

| File | What's inside |
|------|---------------|
| [PLAN.md](PLAN.md) | Product spec, decisions log, design direction, architecture, data model, roadmap, metrics, risks |
| [PHASE_MODEL_MAP.md](PHASE_MODEL_MAP.md) | Which Claude model and session (new or same) to use for each roadmap step |
| [CLAUDE.md](CLAUDE.md) | Instructions for Claude Code in this repo (mandatory model check per step) |
| [reports/Travether competitive research.md](reports/Travether%20competitive%20research.md) | Competitors, UX/style, trust & safety, feature and growth research |
| [research_notes/](research_notes/) | Raw research notes behind the report |
| [design/prototype/](design/prototype/) | Source of the clickable 13-screen prototype ([live canvas](https://claude.ai/artifact/2dn96rPrcF8jKqSQVzFUmQ), private) |

## Tech stack (planned)

Same stack as [kuskus-shel-ima](https://github.com/Meir-Sadon/kuskus-shel-ima):

- **Frontend:** React 19 + Vite + TypeScript, React Router, i18next (English first, Hebrew/RTL ready), PWA with Web Push
- **Backend:** ASP.NET Core (.NET 10) Web API, EF Core + PostgreSQL with PostGIS, SignalR for chat, JWT in httpOnly cookies
- **Infrastructure:** Docker / docker compose locally, Render + Neon Postgres in production, Cloudinary for images
- **Quality:** Vitest + Testing Library, oxlint, xUnit + Testcontainers, GitHub Actions

Details are in [PLAN.md §6](PLAN.md#6-technical-architecture-same-stack-as-kuskus-shel-ima).

## Design

"Fresh Explorer" style: white surfaces, deep ink green `#0E2F2C`, signature lime `#C6F36B` (always with ink text), Rubik font (Latin and Hebrew), 20px rounded cards, bottom navigation. See [PLAN.md §5](PLAN.md#5-design-direction--fresh-explorer).

## Repository layout (planned)

```
frontend/   React + Vite app
backend/    ASP.NET Core API + tests
design/     prototype and design assets
docs/       deployment and other guides
```
