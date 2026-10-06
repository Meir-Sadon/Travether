import { useEffect, useState } from 'react'
import { apiGet, type Health } from './lib/api'

export default function App() {
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
    <main className="app">
      <h1>Travether</h1>
      <p>Find people to do things with on your trip.</p>
      <p className="app__status" role="status">
        API: {health === null ? 'checking…' : health === 'error' ? 'unreachable' : health.status}
      </p>
    </main>
  )
}
