import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

export function NotFoundScreen() {
  const { t } = useTranslation()
  return (
    <div className="screen screen__section">
      <h1>{t('notFound.title')}</h1>
      <p className="screen__meta">{t('notFound.body')}</p>
      <Link to="/" className="btn btn--brand">
        {t('notFound.home')}
      </Link>
    </div>
  )
}
