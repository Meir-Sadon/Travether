import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { apiGet, type Health } from '../lib/api'

/** Placeholder home until the app screens land: shows that the API and database are reachable. */
export function StatusPage() {
  const [health, setHealth] = useState<Health | 'error' | null>(null)

  useEffect(() => {
    const ctrl = new AbortController()
    apiGet<Health>('/health', ctrl.signal)
      .then(setHealth)
      .catch((err: unknown) => {
        if (!ctrl.signal.aborted) {
          console.error(err)
          setHealth('error')
        }
      })
    return () => ctrl.abort()
  }, [])

  return (
    <main style={{ padding: 'var(--space-6) var(--page-gutter)' }}>
      <h1>Travether</h1>
      <p>Find people to do things with on your trip.</p>
      <p role="status">
        API: {health === null ? 'checking…' : health === 'error' ? 'unreachable' : health.status}
        {health && health !== 'error' && ` · database: ${health.database}`}
      </p>
      <p>
        <Link to="/design">Design system</Link>
      </p>
    </main>
  )
}
