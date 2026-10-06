import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { Badge } from '../auth/types'
import { Avatar, Chip, Icon } from '../components'
import { countryName, flag, languageName } from '../lib/countries'
import type { RatingSummary } from '../lib/types'
import '../screens/ProfileScreen.css'

type ProfileViewProps = {
  name: string
  age: number
  countryCode: string
  photoUrl: string | null
  bio: string | null
  languages: string[]
  interests: string[]
  badges: Badge[]
  rating: RatingSummary | undefined
  /** Private name, shown to co-participants and on your own profile. */
  fullName?: string | null
  /** Extra controls next to the avatar (e.g. change photo). */
  avatarAction?: ReactNode
  /** Rendered between the stats and the "About" section. */
  children?: ReactNode
}

const allBadges: { key: Badge; label: 'profile.badge_contact' | 'profile.badge_photo' | 'profile.badge_id' }[] = [
  { key: 'contactVerified', label: 'profile.badge_contact' },
  { key: 'photoVerified', label: 'profile.badge_photo' },
  { key: 'idVerified', label: 'profile.badge_id' },
]

/** Shared layout of a profile: yours and other travelers'. */
export function ProfileView(p: ProfileViewProps) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language

  return (
    <>
      <section className="screen__section profile__head">
        <div className="profile__avatar">
          <Avatar person={{ name: p.name, photoUrl: p.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }} size="lg" />
          {p.avatarAction}
        </div>
        <div>
          <h2 className="profile__name">
            {p.name}, {p.age}
          </h2>
          {p.fullName && <p className="screen__meta">{p.fullName}</p>}
          <p className="screen__meta">
            <span aria-hidden="true">{flag(p.countryCode)}</span> {countryName(p.countryCode, lang)}
          </p>
        </div>
      </section>

      <section className="screen__section">
        <dl className="profile__stats">
          <div>
            <dt>{t('profile.reviews')}</dt>
            <dd>{p.rating?.count ?? '—'}</dd>
          </div>
          <div>
            <dt>{t('profile.rating')}</dt>
            <dd>{p.rating?.average != null ? `${p.rating.average.toFixed(1)} ★` : '—'}</dd>
          </div>
        </dl>
        <p className="screen__meta">{t('profile.ratingRule')}</p>
      </section>

      {p.children}

      <section className="screen__section" aria-labelledby="profile-verify">
        <h2 id="profile-verify" className="screen__section-title">
          {t('profile.verification')}
        </h2>
        <ul className="list-reset screen__stack">
          {allBadges.map((b) => {
            const done = p.badges.includes(b.key)
            return (
              <li key={b.key} className="profile__badge">
                <span className={`profile__badge-icon${done ? ' profile__badge-icon--done' : ''}`}>
                  <Icon name="shieldCheck" size={18} />
                </span>
                <span className="profile__badge-label">{t(b.label)}</span>
                {done ? <Chip tone="success">{t('profile.done')}</Chip> : <span className="screen__meta">{t('profile.notYet')}</span>}
              </li>
            )
          })}
        </ul>
      </section>

      {(p.bio || p.interests.length > 0 || p.languages.length > 0) && (
        <section className="screen__section" aria-labelledby="profile-about">
          <h2 id="profile-about" className="screen__section-title">
            {t('profile.about')}
          </h2>
          {p.bio && <p className="screen__body">{p.bio}</p>}
          {p.interests.length > 0 && (
            <div className="screen__row">
              {p.interests.map((i) => (
                <Chip key={i}>{i18n.exists(`interest.${i}`) ? t(`interest.${i as 'food'}`) : i}</Chip>
              ))}
            </div>
          )}
          {p.languages.length > 0 && (
            <p className="screen__meta">{t('profile.speaks', { languages: new Intl.ListFormat(lang).format(p.languages.map((l) => languageName(l, lang))) })}</p>
          )}
        </section>
      )}
    </>
  )
}
