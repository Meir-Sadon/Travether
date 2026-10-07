import type { PostHog } from 'posthog-js'
import { api } from './api'

/** `ClientConfigDto`: public keys, read at runtime so they change without a rebuild. */
type ClientConfig = { postHogKey: string | null; postHogHost: string; sentryDsn: string | null; environment: string }

/** The funnel from PLAN.md §6: signup → card → plan → request → meetup. Nothing else is tracked. */
export type FunnelEvent = 'signed_up' | 'card_created' | 'plan_created' | 'card_requested' | 'plan_requested' | 'plan_joined' | 'met'

let config: Promise<ClientConfig | null> | null = null
let posthog: PostHog | null = null
let analyticsOn = false
// Steps taken while consent is still loading (right after sign-up); sent if analytics starts, dropped if not.
let pending: [FunnelEvent, Record<string, string | number | boolean> | undefined][] = []

function clientConfig() {
  config ??= api.get<ClientConfig>('/client-config').catch(() => null)
  return config
}

/**
 * Sentry for crashes, when a DSN is configured. No user, cookies, headers, replays or tracing: only the
 * error, the page path and the browser. The SDK loads only when it is needed.
 */
export async function startErrorTracking() {
  const c = await clientConfig()
  if (!c?.sentryDsn) return
  const Sentry = await import('@sentry/react')
  Sentry.init({
    dsn: c.sentryDsn,
    environment: c.environment,
    tracesSampleRate: 0,
    beforeSend(event) {
      delete event.user
      if (event.request) {
        delete event.request.cookies
        delete event.request.headers
        delete event.request.query_string
      }
      return event
    },
  })
}

/**
 * Starts PostHog only for a signed-in user who opted in (Settings → Who sees what, or at sign-up), and
 * stops it otherwise. Events carry the account id, never names or emails.
 */
export async function syncAnalytics(userId: string | null, consent: boolean) {
  analyticsOn = false
  if (!userId || !consent) {
    pending = []
    if (posthog) {
      posthog.opt_out_capturing()
      posthog.reset()
    }
    return
  }
  const c = await clientConfig()
  if (!c?.postHogKey) {
    pending = []
    return
  }
  if (!posthog) {
    posthog = (await import('posthog-js')).default
    posthog.init(c.postHogKey, {
      api_host: c.postHogHost,
      autocapture: false,
      capture_pageview: false,
      capture_pageleave: false,
      disable_session_recording: true,
      person_profiles: 'identified_only',
    })
  }
  posthog.opt_in_capturing()
  posthog.identify(userId)
  analyticsOn = true
  for (const [event, properties] of pending) posthog.capture(event, properties)
  pending = []
}

/** Records a funnel step. A no-op without consent or a PostHog key. */
export function track(event: FunnelEvent, properties?: Record<string, string | number | boolean>) {
  if (analyticsOn) posthog?.capture(event, properties)
  else if (pending.length < 20) pending.push([event, properties])
}
