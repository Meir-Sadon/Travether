import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { apiGet, type Health } from '../lib/api'

/** Placeholder home until the app screens land: shows that the API and database are reachable. */
export function StatusPage() {
  const { t } = useTranslation()
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
      <h1>{t('app.name')}</h1>
      <p>{t('app.tagline')}</p>
      <p role="status">
        {health === null
          ? t('status.api', { state: t('status.checking') })
          : health === 'error'
            ? t('status.api', { state: t('status.unreachable') })
            : t('status.apiWithDatabase', { state: health.status, database: health.database })}
      </p>
      <p>
        <Link to="/design">{t('status.designSystem')}</Link>
      </p>
    </main>
  )
}
