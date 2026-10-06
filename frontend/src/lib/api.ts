export type Health = { status: string; database: string }

/** Small fetch wrapper: same-origin, cookies included, JSON in and out. */
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(`/api${path}`, { credentials: 'include', signal })
  if (!res.ok) throw new Error(`GET ${path} failed with ${res.status}`)
  return (await res.json()) as T
}
