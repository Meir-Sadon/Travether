import { vi } from 'vitest'
import type { Me } from '../auth/types'

export type ApiCall = { method: string; path: string; body: unknown }
type Reply = unknown | ((body: unknown, url: URL) => unknown)

/** A thrown or returned ApiReply sets the status, e.g. `reply(409, { code: 'EmailTaken' })`. */
export class ApiReply {
  readonly status: number
  readonly body?: unknown

  constructor(status: number, body?: unknown) {
    this.status = status
    this.body = body
  }
}

export const reply = (status: number, body?: unknown) => new ApiReply(status, body)

export const testUser: Me = {
  id: 'u-noa',
  displayName: 'Noa',
  fullName: 'Noa Levi',
  email: 'noa@example.com',
  phone: null,
  dateOfBirth: '1996-04-02',
  age: 30,
  countryCode: 'IL',
  photoUrl: null,
  bio: null,
  languages: ['he', 'en'],
  interests: ['hiking', 'food'],
  badges: ['contactVerified'],
  role: 'traveler',
  hasPassword: true,
  createdAt: '2026-09-01T10:00:00Z',
}

/**
 * Replaces fetch with canned API replies keyed by "METHOD /path" (path without /api and query).
 * The session defaults to `testUser`; pass `{ 'GET /auth/me': { user: null } }` for a visitor.
 * Unknown routes answer 404 so a missing mock fails loudly in the assertion, not silently.
 */
export function mockApi(routes: Record<string, Reply> = {}) {
  const calls: ApiCall[] = []
  const table: Record<string, Reply> = { 'GET /auth/me': { user: testUser }, ...routes }

  const fetchMock = vi.fn<typeof fetch>(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(typeof input === 'string' ? input : input.toString(), 'http://localhost')
    const method = (init?.method ?? 'GET').toUpperCase()
    const path = url.pathname.replace(/^\/api/, '')
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : init?.body
    calls.push({ method, path, body })

    const key = `${method} ${path}`
    if (!(key in table)) return json(404, { code: 'NotFound', detail: `No mock for ${key}` })

    let out: unknown
    try {
      const entry = table[key]
      out = typeof entry === 'function' ? (entry as (b: unknown, u: URL) => unknown)(body, url) : entry
    } catch (err) {
      out = err
    }
    if (out instanceof ApiReply) return json(out.status, out.body)
    if (out === undefined) return new Response(null, { status: 204 })
    return json(200, out)
  })

  vi.stubGlobal('fetch', fetchMock)
  return { calls, fetch: fetchMock, set: (key: string, value: Reply) => (table[key] = value) }
}

function json(status: number, body: unknown) {
  return new Response(body === undefined ? null : JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}
