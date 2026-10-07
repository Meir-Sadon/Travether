# Privacy (step 1.12)

PLAN.md §4.9: privacy policy, consent records, data export and account deletion in self-service, and a retention policy. The texts in `frontend/src/legal/documents.ts` are **drafts** that describe what the code below does; privacy counsel reviews them (and a DPIA is done) before public launch.

## Legal pages and consent

- `/legal/terms`, `/legal/privacy` and `/legal/guidelines` are public, so they can be read before signing up.
- Sign-up records `terms`, `privacy_policy` and `community_guidelines` consents with `Auth:LegalVersion`.
- When `Auth:LegalVersion` changes (keep it equal to `LEGAL_VERSION` in `documents.ts`), `GET /api/auth/me` returns `needsConsent: true` and every signed-in screen is replaced by "We updated our terms" until the user accepts (`POST /api/me/consents/legal`) or logs out. Visitor pages stay open.
- Optional consents, off by default: `analytics` (used by step 1.14 before any tracking starts) and `marketing_email` (no marketing email exists yet). `PUT /api/me/consents { kind, granted }`. Withdrawing sets `withdrawn_at`; rows are never deleted, so they prove what was agreed and when.
- `GET /api/me/privacy` returns the current state and the history. Settings → Who sees what shows it with the field visibility rules from [AUTHORIZATION.md](AUTHORIZATION.md).

## Data export

`GET /api/me/export` downloads one JSON file (`travether-data-<date>.json`, `Cache-Control: no-store`), limited to `RateLimits:ExportsPerDay` (5) per user. It contains the account and profile, sign-in methods (provider only), device last-seen dates, consents, notification settings, push services (host only, not the endpoint secret), card memberships, card and plan requests, hosted plans (with the exact meeting point), plans joined, messages sent, "did you meet?" answers, reviews written, published reviews received, notifications, blocks and reports made.

Left out on purpose: other people's personal details (only their ids), password and code hashes, device and ban hashes, and reviews about the user that aren't published yet (they appear once published, at most 14 days later, so the export can't break the double-blind window).

## Account deletion

Settings → Delete account. Confirmed with the password (`POST /api/me/delete { password }`) or with an emailed code (`POST /api/me/delete/code`, then `{ code }`; accounts made with Google, Apple or codes have no password). In one transaction:

| Data | What happens |
|------|--------------|
| Name, email, phone, birth date, country, photo, bio, languages, interests, badges, password | Scrubbed: names empty, email `deleted-<id>@deleted.invalid`, country `ZZ`, photo file deleted. The row stays as a tombstone so messages and reviews keep a sender. |
| Cards the user owns | Pass to the longest-standing co-admin, else member. Cards with nobody else are deleted (open plans cancelled, requests expired). |
| Plans the user hosts that haven't started | Cancelled; participants are notified. |
| Card and plan memberships | `left`; seats in upcoming plans free up. |
| Pending join requests | `withdrawn`. |
| Reviews about the user | Deleted. |
| Google/Apple links, push subscriptions, notifications, notification settings, devices, blocks (both ways), read markers, login codes | Deleted. |
| Consents | Withdrawn, kept as proof. |
| Messages sent, reviews written, "did you meet?" answers | Kept, shown as from "Deleted account": they belong to other people's conversations and ratings. **Counsel to confirm.** |
| Reports made or received, moderation records, ban hashes | Kept (legal obligation and safety; hashes can't be reversed). |

All sessions end (`session_version` bumps), a confirmation email goes to the old address, and the address can sign up again at once.

## Retention

The notification worker applies these every pass (`PrivacyService.ApplyRetentionAsync`):

| Data | Kept for |
|------|----------|
| Notifications | 90 days |
| One-time codes | 1 day after expiry |
| Device ids of accounts that aren't banned | 180 days after last use |
| Everything else | Until the account is deleted (see above) |

Database backups follow the Neon plan's retention and age out on their own.

## Before launch

- Privacy counsel reviews the three texts and the "kept" rows above.
- DPIA (location sharing between strangers, reviews, moderation).
- Name the controller and a real contact address in the privacy policy.
