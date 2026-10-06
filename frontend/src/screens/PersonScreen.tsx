import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { FormError } from '../components'
import { ProfileView } from '../features/ProfileView'
import { ReviewList } from '../features/ReviewList'
import { ScreenHeader } from '../layout/ScreenHeader'
import type { PublicProfile } from '../lib/types'
import { useApi } from '../lib/useApi'

/** Another traveler's profile. The API decides which fields arrive (full name only for co-participants). */
export function PersonScreen() {
  const { t } = useTranslation()
  const { userId } = useParams()
  const { data, error } = useApi<PublicProfile>(`/users/${userId}`)

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
      <div className="screen__section" />
    </div>
  )
}
