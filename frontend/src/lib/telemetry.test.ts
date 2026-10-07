import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mockApi } from '../test/mockApi'

const posthog = vi.hoisted(() => ({
  init: vi.fn<(...args: unknown[]) => void>(),
  identify: vi.fn<(...args: unknown[]) => void>(),
  capture: vi.fn<(...args: unknown[]) => void>(),
  opt_in_capturing: vi.fn<(...args: unknown[]) => void>(),
  opt_out_capturing: vi.fn<(...args: unknown[]) => void>(),
  reset: vi.fn<(...args: unknown[]) => void>(),
}))
vi.mock('posthog-js', () => ({ default: posthog }))

const config = { postHogKey: 'phc_test', postHogHost: 'https://eu.i.posthog.com', sentryDsn: null, environment: 'test' }

describe('telemetry', () => {
  beforeEach(() => {
    vi.resetModules()
    Object.values(posthog).forEach((f) => f.mockClear())
  })

  it('sends nothing without consent', async () => {
    mockApi({ 'GET /client-config': config })
    const { syncAnalytics, track } = await import('./telemetry')
    await syncAnalytics('u-noa', false)
    track('card_created')
    expect(posthog.init).not.toHaveBeenCalled()
    expect(posthog.capture).not.toHaveBeenCalled()
  })

  it('starts with consent, keeps the sign-up step taken while loading, and stops on withdrawal', async () => {
    mockApi({ 'GET /client-config': config })
    const { syncAnalytics, track } = await import('./telemetry')
    track('signed_up')
    await syncAnalytics('u-noa', true)

    expect(posthog.init).toHaveBeenCalledWith('phc_test', expect.objectContaining({ api_host: 'https://eu.i.posthog.com', autocapture: false, disable_session_recording: true }))
    expect(posthog.identify).toHaveBeenCalledWith('u-noa')
    expect(posthog.capture).toHaveBeenCalledWith('signed_up', undefined)

    track('plan_created', { category: 'hike' })
    expect(posthog.capture).toHaveBeenLastCalledWith('plan_created', { category: 'hike' })

    await syncAnalytics('u-noa', false)
    track('met')
    expect(posthog.opt_out_capturing).toHaveBeenCalled()
    expect(posthog.capture).not.toHaveBeenCalledWith('met', undefined)
  })

  it('does nothing when no PostHog key is configured', async () => {
    mockApi({ 'GET /client-config': { ...config, postHogKey: null } })
    const { syncAnalytics, track } = await import('./telemetry')
    await syncAnalytics('u-noa', true)
    track('met')
    expect(posthog.init).not.toHaveBeenCalled()
  })

  it('keeps coordinates and share links out of error reports', async () => {
    const { scrubUrl, withoutQuery } = await import('./telemetry')
    expect(withoutQuery('https://travether.app/api/discover?lat=18.79&lng=98.98')).toBe('https://travether.app/api/discover')
    expect(scrubUrl('/api/places/reverse?lat=1&lng=2#x')).toBe('/api/places/reverse')
    expect(scrubUrl('https://travether.app/c/AbC123xyz?ref=1')).toBe('https://travether.app/c/:slug')
  })
})
