# Clickable mockups (Phase 0)

The key screens from PLAN.md §5 run in the real React app on dummy data ([`frontend/src/mock/data.ts`](../frontend/src/mock/data.ts)), built from the design-system components. They replace the static prototype for testing and become the starting point for the Phase 1 screens, which swap the mock data for API calls.

Run `cd frontend && npm run dev` and open http://localhost:5173, or use the deployed site.

| # | Screen | Route | Try |
|---|--------|-------|-----|
| 1 | Landing / public preview | `/welcome`, `/c/cm-crew-7K2` | Browse without an account; open a shared trip link |
| 2 | Sign up / onboarding (3 steps) | `/signup` | Stepper, 18+ note, private last name, interests |
| 3 | Home | `/` | My trips, matches for your dates |
| 4 | Vacation Card | `/trips/cm-crew` | Plans · Members · Chat; approve Lena; share sheet with a real QR code |
| 5 | Discover | `/discover` | Category chips, list ⇄ map, filters sheet |
| 6 | Activity Plan | `/plans/sanctuary` | Request to join → simulate approval → exact meeting point + chat |
| 7 | Inbox + chat | `/inbox`, `/inbox/sanctuary` | Approve/decline requests; send a message; share your number |
| 8 | Profile | `/profile` | Badges, rating rule, reviews |
| 9 | Create Card / Create Plan | the **+** button | Bottom-sheet forms, location privacy note, seats, audience |
| 10 | Settings | `/settings` | Notification toggles, delete-account confirmation |
| – | "Did you meet?" + review | `/plans/sanctuary/review` | Double-blind note, star rating |

Other routes: `/design` (design system reference), `/status` (API and database health).

## Quick test with 5–10 travelers

PLAN.md asks for a short test before Phase 1. A suggested script, about 15 minutes per person, on their own phone:

1. **First impression (landing).** "What do you think this app is for?" Listen for "dating" or "generic buddy finder"; the goal is "doing things together on a trip".
2. **Find something to do.** "You're in Chiang Mai 12–26 Oct. Find something you'd join this week." Watch whether they find Discover, the categories and the map.
3. **Join.** "Ask to join the elephant sanctuary." Do they understand that the exact meeting point appears only after approval? Is the status stepper clear?
4. **Host.** "You're Noa. Someone asked to join your trip; deal with it." Then: "Create a hike for Tuesday morning."
5. **Share.** "Invite a friend to your trip." Does the link/QR sheet make sense?
6. **Trust.** "Would you meet someone from here? What would make you more comfortable?" Ask about badges, reviews and the safety note.
7. **Wrap-up.** One thing they'd change, and whether they'd use it on their next trip (1–5).

Note where people hesitate, tap the wrong thing or ask a question; those are the fixes before Phase 1. Record results in a short table (person, trip style, tasks completed, top issues) and adjust PLAN.md.
