import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { ScreenHeader } from '../layout/ScreenHeader'
import { LEGAL_VERSION, legalDocs, type LegalDoc } from '../legal/documents'
import { NotFoundScreen } from './NotFoundScreen'

const docs = Object.keys(legalDocs) as LegalDoc[]

/** Terms, privacy policy and community guidelines (PLAN.md §4.9). Public, so they can be read before signing up. */
export function LegalScreen() {
  const { t } = useTranslation()
  const { doc } = useParams()
  if (!docs.includes(doc as LegalDoc)) return <NotFoundScreen />
  const text = legalDocs[doc as LegalDoc]

  return (
    <div className="screen">
      <ScreenHeader back backTo="/" title={text.title} subtitle={t('legal.version', { version: LEGAL_VERSION })} />
      <section className="screen__section">
        <p className="screen__note">{t('legal.draft')}</p>
        <p className="screen__body">
          <strong>{text.summary}</strong>
        </p>
      </section>
      {text.sections.map((s) => (
        <section key={s.heading} className="screen__section">
          <h2 className="screen__section-title">{s.heading}</h2>
          {s.body.map((p) => (
            <p key={p} className="screen__body">
              {p}
            </p>
          ))}
        </section>
      ))}
      <nav className="screen__section" aria-label={t('legal.other')}>
        <ul className="list-reset screen__stack">
          {docs
            .filter((d) => d !== doc)
            .map((d) => (
              <li key={d}>
                <Link to={`/legal/${d}`}>{legalDocs[d].title}</Link>
              </li>
            ))}
        </ul>
      </nav>
    </div>
  )
}
