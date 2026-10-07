import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useSearchParams } from 'react-router'
import { safeNext, useAuth } from '../auth/useAuth'
import { Icon } from '../components'
import { PlanCard } from '../features/PlanCard'
import { plans } from '../mock/data'
import './LandingScreen.css'

/** 1 · Landing / public preview. Visitors can browse before signing up (PLAN.md §3). */
export function LandingScreen() {
  const { t } = useTranslation()
  const { user, refresh } = useAuth()
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))
  const query = next === '/' ? '' : `?next=${encodeURIComponent(next)}`

  const deleted = params.has('deleted')

  // Right after deleting an account the old session is still in memory; reload it (the server has ended it).
  useEffect(() => {
    if (deleted) void refresh()
  }, [deleted, refresh])

  if (user && !deleted) return <Navigate to={next} replace />

  return (
    <div className="screen landing">
      <header className="landing__bar">
        <span className="landing__brand">
          <span className="landing__logo" aria-hidden="true">
            <Icon name="users" size={18} />
          </span>
          {t('app.name')}
        </span>
        <Link to={`/login${query}`} className="landing__login">
          {t('landing.logIn')}
        </Link>
      </header>

      <section className="screen__section">
        <h1 className="landing__title">
          {t('landing.titleStart')} <mark>{t('landing.titleHighlight')}</mark>
        </h1>
        <p className="landing__lead">{t('landing.lead')}</p>
        {deleted && (
          <p className="screen__note" role="status">
            {t('landing.deleted')}
          </p>
        )}
        <div className="landing__hero" aria-hidden="true">
          <svg viewBox="0 0 350 200" preserveAspectRatio="xMidYMid slice">
            <circle cx="270" cy="62" r="26" fill="#F6D7A7" />
            <path d="M0 150 L70 90 L120 130 L190 70 L260 128 L310 100 L350 120 L350 200 L0 200Z" fill="#8FB8BE" />
            <path d="M0 170 L90 128 L160 160 L240 122 L350 160 L350 200 L0 200Z" fill="#5E8F96" />
          </svg>
          <span className="landing__pill">
            <Icon name="pin" size={14} />
            {t('landing.heroPill', { city: 'Chiang Mai', count: 23 })}
          </span>
        </div>
      </section>

      <section className="screen__section" aria-labelledby="landing-week">
        <h2 id="landing-week" className="screen__section-title">
          {t('landing.thisWeek')}
        </h2>
        <ul className="list-reset screen__stack">
          {plans.slice(0, 2).map((p) => (
            <PlanCard key={p.id} plan={p} />
          ))}
        </ul>
        <p className="landing__trust">
          <span>
            <Icon name="shieldCheck" size={15} />
            {t('landing.verified')}
          </span>
          <span>
            <Icon name="chat" size={15} />
            {t('landing.chatFirst')}
          </span>
        </p>
      </section>

      <footer className="screen__footer">
        <Link to={`/signup${query}`} className="btn btn--primary btn--lg btn--block">
          {t('landing.getStarted')}
        </Link>
        <Link to="/discover" className="btn btn--ghost btn--block">
          {t('landing.browse')}
        </Link>
      </footer>
    </div>
  )
}
