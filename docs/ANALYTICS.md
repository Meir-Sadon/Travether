# Analytics and error tracking (step 1.14)

PLAN.md §6: PostHog (EU) for the funnel **signup → card → plan → request → meetup**, Sentry for errors. Both are off until their keys are set ([DEPLOY.md](DEPLOY.md)); the browser reads the keys at runtime from `GET /api/client-config`, so no rebuild is needed.

## Consent

Analytics is **opt-in** ([PRIVACY.md](PRIVACY.md)): an unticked box at sign-up (`allowAnalytics`) and a switch in Settings → Who sees what. `GET /api/auth/me` returns `analytics`, and `syncAnalytics` in `frontend/src/lib/telemetry.ts` starts PostHog only when it is true, and opts out and resets when it turns false or the user logs out. Visitors are never tracked. The PostHog SDK is only downloaded once someone has opted in.

## Events

Only these, from `track()`; no autocapture, pageviews, session recording or surveys.

| Event | When | Properties |
|-------|------|------------|
| `signed_up` | Account created (queued until consent loads) | |
| `card_created` | Vacation Card created | `visibility` |
| `plan_created` | Activity Plan created | `category`, `audience` |
| `card_requested` | Request to join a card | |
| `plan_requested` | Request to join a plan | `category` |
| `plan_joined` | Card member joins a plan directly | `category` |
| `met` | "Did you meet?" answered yes | |

Events are identified by the account id. In PostHog, build one funnel with these steps in this order. In the project settings, turn on "Discard client IP data", and keep the data region in the EU.

## Errors

- Browser: `@sentry/react` is loaded only when `Telemetry:SentryDsn` is set. Events leave out the user, cookies, headers and query strings; tracing and replay are off.
- API: `Sentry.AspNetCore` is used when `Sentry:Dsn` is set, with `SendDefaultPii = false`, no request bodies, cookies or user, and tracing off.

`Telemetry:Environment` tags both (defaults to the ASP.NET environment name).
