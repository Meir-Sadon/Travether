import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, FormError } from '../components'
import { ProfileView } from '../features/ProfileView'
import { ReportSheet, type ReportTarget } from '../features/ReportSheet'
import { ReviewList } from '../features/ReviewList'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import type { PublicProfile } from '../lib/types'
import { useApi } from '../lib/useApi'

/** Another traveler's profile. The API decides which fields arrive (full name only for co-participants). */
export function PersonScreen() {
  const { t } = useTranslation()
  const { userId } = useParams()
  const { data, error } = useApi<PublicProfile>(`/users/${userId}`)
  const { user } = useAuth()
  const navigate = useNavigate()
  const [reporting, setReporting] = useState<ReportTarget | null>(null)
  const [confirmBlock, setConfirmBlock] = useState(false)
  const [blockError, setBlockError] = useState<string | null>(null)
  const canAct = !!user && !!data && user.id !== data.id

  const block = async () => {
    try {
      await api.post(`/users/${userId}/block`)
      void navigate(-1)
    } catch (err) {
      setBlockError(errorCode(err))
    }
  }

  return (
    <div className="screen">
      <ScreenHeader back title={data?.displayName ?? t('profile.title')} />
      {error && (
        <div className="screen__section">
          <FormError code={error} />
        </div>
      )}
      {!data && !error && (
        <p className="screen__loading" role="status">
          {t('common.loading')}
        </p>
      )}
      {data && (
        <ProfileView
          name={data.displayName}
          age={data.age}
          countryCode={data.countryCode}
          photoUrl={data.photoUrl}
          bio={data.bio}
          languages={data.languages}
          interests={data.interests}
          badges={data.badges}
          rating={data.rating}
          fullName={data.fullName}
        />
      )}
      {data && <ReviewList userId={data.id} />}
      {canAct && (
        <section className="screen__section screen__row">
          <Button variant="ghost" size="sm" onClick={() => setReporting({ type: 'user', id: data.id })}>
            {t('report.title_user')}
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setConfirmBlock(true)}>
            {t('blocked.block', { name: data.displayName })}
          </Button>
        </section>
      )}
      <ReportSheet target={reporting} onClose={() => setReporting(null)} />
      <BottomSheet
        open={confirmBlock}
        onClose={() => setConfirmBlock(false)}
        title={t('blocked.confirmTitle', { name: data?.displayName ?? '' })}
        footer={
          <div className="screen__stack">
            <Button block size="lg" onClick={() => void block()}>
              {t('blocked.confirm')}
            </Button>
            <Button block variant="ghost" onClick={() => setConfirmBlock(false)}>
              {t('blocked.keep')}
            </Button>
          </div>
        }
      >
        <p className="screen__body">{t('blocked.confirmBody')}</p>
        <FormError code={blockError} />
      </BottomSheet>
      <div className="screen__section" />
    </div>
  )
}
