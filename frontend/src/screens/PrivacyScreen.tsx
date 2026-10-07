import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { FormError, Icon } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { legalDocs, type LegalDoc } from '../legal/documents'
import { api, errorCode } from '../lib/api'
import type { Privacy } from '../lib/types'
import { useApi } from '../lib/useApi'
import './SettingsScreen.css'

const audiences = ['everyone', 'approved', 'onlyYou'] as const

/** Settings → Who sees what (PLAN.md §4.1, §4.9): field visibility, optional consents, data download, legal texts. */
export function PrivacyScreen() {
  const { t, i18n } = useTranslation()
  const { data, setData } = useApi<Privacy>('/me/privacy')
  const [error, setError] = useState<string | null>(null)

  const setAnalytics = async (granted: boolean) => {
    setError(null)
    try {
      setData(await api.put<Privacy>('/me/consents', { kind: 'analytics', granted }))
    } catch (err) {
      setError(errorCode(err))
    }
  }

  return (
    <div className="screen">
      <ScreenHeader back backTo="/settings" title={t('privacy.title')} />

      <section className="screen__section" aria-labelledby="privacy-who">
        <h2 id="privacy-who" className="screen__section-title">
          {t('privacy.whoTitle')}
        </h2>
        <ul className="list-reset settings__list">
          {audiences.map((a) => (
            <li key={a} className="settings__info">
              <strong>{t(`privacy.who_${a}`)}</strong>
              <span className="screen__meta">{t(`privacy.who_${a}_body`)}</span>
            </li>
          ))}
        </ul>
      </section>

      <section className="screen__section" aria-labelledby="privacy-choices">
        <h2 id="privacy-choices" className="screen__section-title">
          {t('privacy.choicesTitle')}
        </h2>
        {data && (
          <ul className="list-reset settings__list">
            <li>
              <label className="settings__toggle">
                <span>
                  {t('privacy.analytics')}
                  <span className="screen__meta">{t('privacy.analytics_hint')}</span>
                </span>
                <input type="checkbox" role="switch" aria-checked={data.analytics} checked={data.analytics} onChange={(e) => void setAnalytics(e.target.checked)} />
              </label>
            </li>
          </ul>
        )}
        <FormError code={error} />
      </section>

      <section className="screen__section" aria-labelledby="privacy-data">
        <h2 id="privacy-data" className="screen__section-title">
          {t('privacy.dataTitle')}
        </h2>
        <p className="screen__body">{t('privacy.export_body')}</p>
        <ul className="list-reset settings__list">
          <li>
            {/* A plain link: the session cookie goes along and the browser saves the file. */}
            <a className="settings__row" href="/api/me/export" download>
              {t('settings.export')}
              <Icon name="forward" size={18} />
            </a>
          </li>
          {(Object.keys(legalDocs) as LegalDoc[]).map((d) => (
            <li key={d}>
              <Link to={`/legal/${d}`} className="settings__row">
                {legalDocs[d].title}
                <Icon name="forward" size={18} />
              </Link>
            </li>
          ))}
        </ul>
        {data && (
          <p className="screen__meta">
            {t('privacy.accepted', { version: data.legalVersion, date: acceptedOn(data, i18n.language) })}
          </p>
        )}
      </section>
    </div>
  )
}

function acceptedOn(p: Privacy, locale: string) {
  const latest = p.history.find((h) => h.kind === 'terms' && h.version === p.legalVersion)
  return latest ? new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(latest.grantedAt)) : '—'
}
