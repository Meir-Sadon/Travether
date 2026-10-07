# End-to-end tests (step 1.15)

Playwright drives Chromium (phone-sized) through the core flows against the real API, a PostGIS database and the Vite dev server:

| Spec | Flow |
|------|------|
| `onboarding.spec.ts` | Sign up in three steps, log out, log back in |
| `trip-and-plan.spec.ts` | Create a trip, share the invite link, request and approve, post a plan, visitor sees only the area, member joins and sees the meeting point, live chat |
| `plan-request.spec.ts` | An outsider asks for a seat, the host approves, the meeting point appears |
| `privacy.spec.ts` | Download my data, delete the account, the old password stops working |

Meeting-point search is answered in the browser (`stubPlaces`), so no outside geocoder is called. Every test signs up fresh users, so the database can be reused between runs.

## Run locally

```bash
docker run -d --name travether-e2e-db -p 5433:5432 \
  -e POSTGRES_DB=travether_e2e -e POSTGRES_USER=travether -e POSTGRES_PASSWORD=travether \
  postgis/postgis:17-3.5
cd frontend && npm ci && cd ../e2e && npm ci
npx playwright install chromium
npm test
```

Playwright starts the API on :5080 (`dotnet run`, migrations applied on start) and Vite on :5173, or reuses ones already running. Set `E2E_DATABASE_URL` for another database and `E2E_CHROMIUM` to use an installed Chromium. On failure, `npm run report` opens traces and screenshots. CI runs the same suite in the `e2e` job.
