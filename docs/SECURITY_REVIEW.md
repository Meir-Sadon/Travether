# Pre-launch security review (step 1.16)

A read-through of the whole MVP before public launch, in four parts: sign-in and sessions, access control (who can see and change what), web and infrastructure, and privacy. Every finding was traced through the code before it was fixed. Each fix has a regression test, most of them in `backend/tests/Travether.Api.Tests/SecurityTests.cs`.

Nothing critical turned up. Dependencies were clean: `npm audit --omit=dev` and `dotnet list package --vulnerable` reported nothing.

## Fixed

| # | Severity | Area | Problem | Fix |
|---|----------|------|---------|-----|
| 1 | High | Sign-in | Someone could register another person's email with a password of their choosing. When the real owner later signed in with an emailed code or with Google/Apple, they landed in that account, and the squatter's password and session still worked. | The first time an address is proven on an unverified account, the password is dropped and every session ends (`AuthController.ClaimAddress`). |
| 2 | Medium | Sign-in | Parallel guesses could get past the 5-guess limit on emailed codes, because the attempt count was read and written back without a lock. | The attempt is counted in one conditional `UPDATE` before comparing, and only one request can consume a code (`LoginCodeService.VerifyAsync`). |
| 3 | Medium | Sign-in | Each IPv6 address got its own rate-limit bucket, so one /64 gave an attacker endless addresses. | Anonymous rate limits group IPv6 clients by /64 (`ClientKey`). |
| 4 | Medium | Sessions | An open chat connection kept receiving messages after "log out everywhere", a password reset, a ban or account deletion. | Open connections are tracked and closed within 30 seconds once their session ends (`HubSessions`, `HubSessionSweeper`). |
| 5 | Medium | Access | A card member whom the host had blocked could still join the host's plan, see the exact meeting point and use its chat, or add the host to their own plan. | A block with the host now hides the plan even from members of the host's card (`AccessRules.PlanAccessFor`), and plans can't be created with blocked participants. |
| 6 | Medium | Access | A member removed from a card (or one who left) kept full host powers over the plans they hosted there: chat, the exact point, editing and approving strangers. | Their upcoming plans in that card are cancelled and participants are told (`CardMembershipController.EndMembershipAsync`). |
| 7 | Medium | Web | No browser security headers, so the app could be framed (clickjacking) and had no HSTS. | `X-Frame-Options: DENY`, `frame-ancestors 'none'`, `object-src 'none'`, `base-uri 'self'`, `nosniff`, `Referrer-Policy` and `Permissions-Policy` on every response. HSTS for one year in production. |
| 8 | Medium | Privacy | Without a Resend key, production logged every email body, including sign-in, reset and delete codes. Sentry could then pick these lines up as breadcrumbs. | Email bodies are only logged in Development and Testing. Elsewhere an error says that no email was sent. |
| 9 | Medium | Privacy | Phone and WhatsApp numbers shared in chat survived account deletion. | Deletion empties and hides those messages. |
| 10 | Medium | Privacy | A deleted user's name and message previews stayed in other people's notifications. | Payloads carry `actorId`, and deletion strips `actor`, `preview` and `actorId` from them. |
| 11 | Medium | Privacy | The data export included other people's names and previews of their chat messages through notifications. | Chat notifications are left out of the export. The rest keep only link, subject and count. |
| 12 | Medium | Privacy | Sentry reports and breadcrumbs carried URL queries (device coordinates on `/api/places/reverse`, `/api/discover` and `/plans/:id`) and invite-only share links. | Queries are stripped on both sides, and `/c/<slug>` becomes `/c/:slug` in browser breadcrumbs. |
| 13 | Low | Access | A seat-limit edit could race with an approval and overbook a plan. | Plan edits lock the plan row, like join and approve already did. |
| 14 | Low | Access | Suspended users still showed up in plan participants, request parties and My Cards previews. | They are filtered out of those lists. |
| 15 | Low | Web | Request bodies could reach Kestrel's default of 30 MB, which is enough to exhaust the 512 MB instance. | 256 KB by default. Photo uploads keep their own higher limit. |
| 16 | Low | Web / CI | The session cookie's `Secure` flag depended on proxy headers. The CI token had the repository's default permissions. Push endpoint URLs went into the HttpClient logs. `/\host` was accepted as a "next" path. | `Secure` is always on in production. CI has `permissions: contents: read`. HttpClient logs at Warning. `safeNext` rejects `/\`. |

The draft privacy policy (`frontend/src/legal/documents.ts`) now covers three things: Photon receives coordinates and not only search text, shared phone numbers and notification names go with a deleted account, and PostHog keeps only the account id. Remembered Discover search places are cleared from the device on logout.

## Accepted for launch

- **`POST /api/auth/register` reveals whether an email has an account** (`EmailTaken`). The code and Google/Apple flows reveal nothing. Closing this needs an emailed "you already have an account" flow. Revisit if password sign-up becomes the main path.
- **Plain logout only deletes the cookie.** A copied token stays valid for its 30-day lifetime unless the user picks "log out everywhere". A token deny-list or shorter sessions with refresh would fix it.
- **Google/Apple ID tokens have no nonce.** A stolen ID token could be replayed until it expires, which is minutes. That only matters after the token was already stolen.

## Follow-ups (not done in this step)

1. **Full Content-Security-Policy.** The current policy only limits framing, plugins and `<base>`. An allow-list for scripts and connections (Google and Apple sign-in, Sentry, PostHog, Cloudinary) has to be tried on a deployed build first, because the e2e tests run against the Vite dev server, which doesn't send these headers.
2. **Deleted trips are only soft-deleted.** Names, descriptions, plans with exact meeting points and their chats stay in the database, and the cover image stays on Cloudinary. Add a retention step that hard-deletes cards and plans some days after `deleted_at`, and destroy the cover when a card is deleted.
3. **Past plans of a deleted host** still show a host with an empty name and a nonsense age. Show "Deleted account" instead.
4. **Cloudinary deletes** don't check the result and don't invalidate the CDN cache.
5. **PostHog data** is not deleted with the account. Call PostHog's person deletion on account deletion.
6. **Suspended users still hold plan seats.** Free them when the ban is applied.
7. **"Groups only" plans** accept a request from a one-person card. That matches AUTHORIZATION.md, but it may not be what "groups only" should mean. This is a product decision.

## Check on the live deploy

- **Client IP.** The app trusts `X-Forwarded-For` from any proxy and uses its last entry. Every per-IP limit (sign-in, sign-up, place search) depends on that entry being the real client address. After the first deploy, send a request with a made-up `X-Forwarded-For` header and log `RemoteIpAddress`. If the made-up value comes through, or every request shows the same Render address, set `ForwardLimit` or the trusted proxy ranges to match.
- **`Email__ResendApiKey` is set.** Without it no sign-in code reaches anyone.
- **`AuthCookie__SameSite` in `render.yaml`** isn't read by the code. The cookie is always `SameSite=Lax`. Remove the setting or wire it up.

## Checked and sound

- **Session tokens:**
  - HMAC-SHA256 with a pinned algorithm, plus issuer, audience and lifetime checks.
  - Sign-up tokens use their own audience, so they can't be used as session tokens.
  - Every request re-checks ban, deletion and session version.
  - No signing-key fallback outside Development and Testing.
- **CSRF:** a custom header is required on every non-GET `/api` request. There is no CORS, the cookie is SameSite=Lax, and no GET endpoint changes anything.
- **Emailed codes:** 6 digits from a secure random generator, stored as keyed hashes and compared in constant time. They expire after 10 minutes, only the newest one works, and at most 5 are sent per hour per address.
- **Google/Apple:** signature, issuer, audience and expiry are all verified, and `email_verified` is required before linking by email.
- **Authorization:**
  - Every write needs sign-in.
  - Withdraw, approve and reject are scoped to the caller's own card or plan, so other people's records can't be reached by id.
  - Roles can't be escalated, and moderator rights are re-checked in the database.
  - The exact meeting point only reaches participants, including through chat, `.ics` files and the export.
  - Blocks apply in both directions everywhere else.
- **Input handling:**
  - Uploads: file type checked from the content, size capped, file names generated by the server.
  - No SQL built from strings.
  - React escapes all output, and nothing sets `innerHTML`.
  - `wa.me` and `tel:` links are digits only.
  - The push endpoint allow-list blocks server-side request forgery.
  - `.ics` files are escaped and folded.
- **Operations:**
  - The container runs as non-root and has no baked-in secrets.
  - Production error responses carry no stack traces, and OpenAPI is only served in Development.
  - Sentry gets no user, cookies or bodies.
  - PostHog loads only after consent, with autocapture and replay off.
- **Retention** matches [PRIVACY.md](PRIVACY.md), and account deletion scrubs what the policy says it does.
