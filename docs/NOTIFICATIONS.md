# Notifications

PLAN.md §4.7. Code: [`backend/src/Travether.Api/Notifications`](../backend/src/Travether.Api/Notifications), service worker [`frontend/public/sw.js`](../frontend/public/sw.js).

## How a notification travels

1. An endpoint commits its change, then calls `Notifier` (e.g. `PlanRequestedAsync`). It writes a `notifications` row per recipient and pings the open app over SignalR (`notification` event on `/hubs/chat`) so the Home bell and Inbox badge update.
2. `NotificationWorker` (a background service) wakes up right away and runs `NotificationDelivery`: for each queued row it checks the user's settings and sends Web Push and/or email, then sets `delivered_at`.
3. Every 5 minutes the worker also runs `NotificationJobs`: plan reminders and the daily digest.

Rows older than 12 hours that were never delivered (say, the server was down) are marked done without sending.

## What goes where

| Type | Category | In-app | Push | Email |
|------|----------|:---:|:---:|:---:|
| `card_request`, `plan_request` (to the people who can decide) | Requests | ✔ | ✔ | ✔ |
| `card_request_approved/rejected`, `plan_request_approved/rejected` (to the requester; approval also to their party) | Requests | ✔ | ✔ | ✔ |
| `plan_joined` (a card member took a seat; to the host) | Requests | ✔ | ✔ | |
| `plan_cancelled` (to everyone going except whoever cancelled) | Reminders | ✔ | ✔ | ✔ |
| `plan_reminder` (24 h and 2 h before; skipped when the plan was created inside that window) | Reminders | ✔ | ✔ | |
| `chat_message` | Messages | Inbox unread only | ✔ batched | |
| `matches_digest` (new plans near a current or upcoming trip, posted in the last 24 h) | Matches | ✔ | ✔ | ✔ |

- **Settings:** a category that is off still shows in the app (the digest is simply not made) but sends no push or email. Email has its own switch.
- **Quiet hours** (default 22:00 to 08:00 in the user's zone) hold back push only; in-app and email still arrive. The zone is reported by the device when settings are saved or push is turned on.
- **Chat batching:** at most one push per conversation per 10 minutes per person (`dedupe_key`), and the device replaces the previous one (same tag and Web Push `Topic`).
- **Digest:** sent once a day per trip from 08:00 local time (`Notifications:DigestHour`), using Discover's rules: within 30 km, dates inside the trip, not your own trips, not plans you joined, no blocked hosts. Cards have regions, not coordinates, so the first region is geocoded once and stored in `vacation_cards.area`.
- Review reminders ("Did you meet?") arrive with step 1.10.

## Web Push

- Standard Web Push: VAPID (RFC 8292) and aes128gcm encryption (RFC 8291), implemented with .NET's own crypto (`WebPush.cs`), no extra package.
- Subscriptions are accepted only for the known push services (Google FCM, Mozilla, Microsoft, Apple), so the server never posts to an arbitrary URL. A push service answering 404/410 deletes the subscription. Each user keeps up to 10 devices.
- iPhone and iPad: push works only after **Add to Home Screen** (iOS 16.4+). Settings explains this when Safari can't subscribe.
- Keys: `Push:VapidPublicKey` / `Push:VapidPrivateKey` ([DEPLOY.md](DEPLOY.md)). In Development, throwaway keys are generated at startup if none are set.

## Running more than one API instance

Delivery and the jobs assume one instance. Dedupe keys keep reminders and digests from doubling, but two instances could send the same push twice. Before scaling out, claim rows with `FOR UPDATE SKIP LOCKED` or move the worker to its own service.
