export type Health = { status: string; database: string }

/** Every state-changing request carries this header; the API rejects them without it (CSRF guard). */
export const CSRF_HEADER = 'X-Travether-Csrf'

/** An API failure with the server's stable error code (e.g. `EmailTaken`), mapped to text via `errors.<code>`. */
export class ApiError extends Error {
  readonly status: number
  readonly code: string

  constructor(status: number, code: string) {
    super(`${status} ${code}`)
    this.status = status
    this.code = code
  }
}

async function request<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (method !== 'GET') headers[CSRF_HEADER] = '1'
  if (body !== undefined && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'

  const res = await fetch(`/api${path}`, {
    method,
    credentials: 'include',
    headers,
    body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
    signal,
  })

  if (!res.ok) {
    let code = res.status === 401 ? 'Unauthorized' : res.status === 404 ? 'NotFound' : 'Unknown'
    try {
      const problem = (await res.json()) as { code?: string; errors?: unknown }
      if (problem.code) code = problem.code
      else if (problem.errors) code = 'Invalid'
    } catch {
      // not JSON
    }
    throw new ApiError(res.status, code)
  }

  if (res.status === 204 || res.status === 202) return undefined as T
  const text = await res.text()
  return (text ? JSON.parse(text) : undefined) as T
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>('GET', path, undefined, signal),
  post: <T = void>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T = void>(path: string, body?: unknown) => request<T>('PUT', path, body ?? {}),
  patch: <T = void>(path: string, body: unknown) => request<T>('PATCH', path, body),
  del: <T = void>(path: string, body?: unknown) => request<T>('DELETE', path, body),
}

/** Kept for the status page. */
export function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  return api.get<T>(path, signal)
}

/** The error code to translate, for any thrown value. */
export function errorCode(err: unknown): string {
  return err instanceof ApiError ? err.code : 'Network'
}
