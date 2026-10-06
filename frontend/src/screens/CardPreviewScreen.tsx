import { useTranslation } from 'react-i18next'
import { Link, Navigate, useParams } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { FormError } from '../components'
import { JoinRequest } from '../features/JoinRequest'
import type { Card } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import { TripPreview } from './TripScreen'

/** 1 · A card opened from its share link or QR code. Works without an account (PLAN.md §3). */
export function CardPreviewScreen() {
  const { t } = useTranslation()
  const { slug } = useParams()
  const { user } = useAuth()
  const { data: card, error, setData } = useApi<Card>(user === undefined ? null : `/cards/share/${slug}`)

  if (error === 'NotFound') return <NotFoundScreen />
  if (!card) {
    return (
      <p className="screen__loading" role="status">
        {error ? <FormError code={error} /> : t('common.loading')}
      </p>
    )
  }
  if (card.access !== 'preview') return <Navigate to={`/trips/${card.id}`} replace />

  const here = `/c/${slug}`
  return (
    <TripPreview
      card={card}
      footer={
        user ? (
          <JoinRequest card={card} shareSlug={slug} onChange={setData} />
        ) : (
          <Link to={`/signup?next=${encodeURIComponent(here)}`} className="btn btn--primary btn--lg btn--block">
            {t('trip.signUpToJoin')}
          </Link>
        )
      }
    />
  )
}
