import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Avatar, Button, Card, CardBody, Chip, Icon } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { me, reviewsAboutMe } from '../mock/data'
import './ProfileScreen.css'

/** 8 · Profile: badges, rating (3+ reviews), reviews. */
export function ProfileScreen() {
  const { t } = useTranslation()
  const [idDone, setIdDone] = useState(false)

  const badges = [
    { key: 'contact', done: true },
    { key: 'photo', done: true },
    { key: 'id', done: idDone },
  ] as const

  return (
    <div className="screen">
      <ScreenHeader
        large
        title={t('profile.title')}
        action={
          <Link to="/settings" className="icon-btn" aria-label={t('settings.title')}>
            <Icon name="settings" />
          </Link>
        }
      />
      <section className="screen__section profile__head">
        <Avatar person={{ name: me.name, tint: me.tint }} size="lg" />
        <div>
          <h2 className="profile__name">
            {me.name}, {me.age}
          </h2>
          <p className="screen__meta">
            {me.flag} {me.country}
          </p>
        </div>
      </section>

      <section className="screen__section">
        <dl className="profile__stats">
          <div>
            <dt>{t('profile.reviews')}</dt>
            <dd>{me.reviewCount}</dd>
          </div>
          <div>
            <dt>{t('profile.rating')}</dt>
            <dd>{me.rating !== null ? `${me.rating} ★` : '—'}</dd>
          </div>
          <div>
            <dt>{t('profile.meetups')}</dt>
            <dd>7</dd>
          </div>
        </dl>
        <p className="screen__meta">{t('profile.ratingRule')}</p>
      </section>

      <section className="screen__section" aria-labelledby="profile-verify">
        <h2 id="profile-verify" className="screen__section-title">
          {t('profile.verification')}
        </h2>
        <ul className="list-reset screen__stack">
          {badges.map((b) => (
            <li key={b.key} className="profile__badge">
              <span className={`profile__badge-icon${b.done ? ' profile__badge-icon--done' : ''}`}>
                <Icon name="shieldCheck" size={18} />
              </span>
              <span className="profile__badge-label">{t(`profile.badge_${b.key}`)}</span>
              {b.done ? (
                <Chip tone="success">{t('profile.done')}</Chip>
              ) : (
                <Button size="sm" variant="brand" onClick={() => setIdDone(true)}>
                  {t('profile.verify')}
                </Button>
              )}
            </li>
          ))}
        </ul>
      </section>

      <section className="screen__section" aria-labelledby="profile-about">
        <h2 id="profile-about" className="screen__section-title">
          {t('profile.about')}
        </h2>
        <p className="screen__body">{t('profile.bio')}</p>
        <div className="screen__row">
          {(['hiking', 'food', 'dayTrips'] as const).map((i) => (
            <Chip key={i}>{t(`interest.${i}`)}</Chip>
          ))}
        </div>
        <p className="screen__meta">{t('profile.speaks', { languages: me.languages.join(', ') })}</p>
      </section>

      <section className="screen__section" aria-labelledby="profile-reviews">
        <h2 id="profile-reviews" className="screen__section-title">
          {t('profile.whatPeopleSay')}
        </h2>
        <ul className="list-reset screen__stack">
          {reviewsAboutMe.map((r) => (
            <Card as="li" key={r.from} variant="filled">
              <CardBody>
                <span aria-label={t('profile.stars', { count: r.stars })} className="profile__stars">
                  {'★'.repeat(r.stars)}
                  {'☆'.repeat(5 - r.stars)}
                </span>
                <p className="screen__body">“{r.text}”</p>
                <span className="screen__meta">
                  {r.from} · {r.country} · {r.date}
                </span>
              </CardBody>
            </Card>
          ))}
        </ul>
      </section>
      <div className="screen__section" />
    </div>
  )
}
