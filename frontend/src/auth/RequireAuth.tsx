import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from './useAuth'

/** Sends visitors to the landing page, remembering where they wanted to go. */
export function RequireAuth() {
  const { user } = useAuth()
  const location = useLocation()
  const { t } = useTranslation()

  if (user === undefined) {
    return (
      <p className="screen__loading" role="status">
        {t('common.loading')}
      </p>
    )
  }

  if (user === null) {
    const next = location.pathname + location.search
    return <Navigate to={`/welcome?next=${encodeURIComponent(next)}`} replace />
  }

  return <Outlet />
}
