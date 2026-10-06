import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useMe } from '../auth/useAuth'
import { AvatarStack, BottomSheet, Button, Card, CardBody, Chip, Icon, TextField } from '../components'
import { CreateSheet } from '../features/CreateSheet'
import { PlanTile } from '../features/PlanTile'
import { ScreenHeader } from '../layout/ScreenHeader'
import { countryName } from '../lib/countries'
import { formatDateRange, tintFor, todayIso } from '../lib/dates'
import { useNotificationEvents } from '../lib/chatHub'
import { discoverPath, useTripOrigin } from '../lib/discover'
import { shareCodeFrom } from '../lib/share'
import type { DiscoverPlan, MyCard, PendingWrapUp } from '../lib/types'
import { useApi } from '../lib/useApi'
import './HomeScreen.css'
import './InboxScreen.css'
import './NotificationsScreen.css'

/** 3 · Home: my trips + plans matching my dates nearby. */
export function HomeScreen() {
  const { t, i18n } = useTranslation()
  const me = useMe()
  const navigate = useNavigate()
  const [creating, setCreating] = useState(false)
  const [joining, setJoining] = useState(false)
  const [code, setCode] = useState('')
  const { data: myTrips } = useApi<MyCard[]>('/cards')
  const today = todayIso()
  const current = myTrips?.find((c) => c.endsOn >= today)
  const origin = useTripOrigin(current ?? null, i18n.language)
  const { data: matches } = useApi<DiscoverPlan[]>(
    current && origin ? discoverPath({ origin, from: current.startsOn > today ? current.startsOn : today, to: current.endsOn }) : null,
  )
  const nearby = matches?.slice(0, 8) ?? []
  const unread = useApi<{ count: number }>('/notifications/unread')
  useNotificationEvents(() => unread.reload())
  const unreadCount = unread.data?.count ?? 0
  const { data: wrapUps } = useApi<PendingWrapUp[]>('/me/wrap-ups')

  const join = (e: FormEvent) => {
    e.preventDefault()
    const slug = shareCodeFrom(code)
    if (slug) void navigate(`/c/${slug}`)
  }

  return (
    <div className="screen">
      <ScreenHeader
        large
        title={t('home.greeting', { name: me.displayName })}
        subtitle={current ? `${current.regions[0]} · ${formatDateRange(current.startsOn, current.endsOn, i18n.language)}` : t('home.noTripSubtitle')}
        action={
          <Link to="/notifications" className="home__bell" aria-label={unreadCount ? t('home.notificationsUnread', { count: unreadCount }) : t('home.notifications')}>
            <Icon name="bell" size={24} />
            {unreadCount > 0 && <span className="home__bell-dot" />}
          </Link>
        }
      />

      {wrapUps && wrapUps.length > 0 && (
        <section className="screen__section" aria-labelledby="home-wrapups">
          <h2 id="home-wrapups" className="screen__section-title">
            {t('home.wrapUpsTitle')}
          </h2>
          {wrapUps.map((w) => (
            <Link key={w.planId} to={`/plans/${w.planId}/review`} className="inbox__prompt">
              <strong>{w.needsAnswer ? t('home.wrapUpAnswer', { title: w.title }) : t('home.wrapUpReview', { count: w.toReview, title: w.title })}</strong>
            </Link>
          ))}
        </section>
      )}

      <section className="screen__section" aria-labelledby="home-trips">
        <h2 id="home-trips" className="screen__section-title">
          {t('home.myTrips')}
        </h2>
        {myTrips?.length === 0 && <p className="screen__empty">{t('home.noTrips')}</p>}
        {myTrips?.map((tr) => (
          <Card key={tr.id} interactive className="trip-tile">
            <Link to={`/trips/${tr.id}`} className="trip-tile__link">
              <div
                className="trip-tile__cover"
                style={{ background: tr.coverUrl ? `center / cover no-repeat url("${tr.coverUrl}")` : tintFor(tr.id) }}
              >
                <Chip tone="inverse">
                  {countryName(tr.countryCode, i18n.language)} · {formatDateRange(tr.startsOn, tr.endsOn, i18n.language)}
                </Chip>
              </div>
              <CardBody>
                <strong className="trip-tile__name">{tr.name}</strong>
                <span className="screen__meta">{tr.regions.join(' · ')}</span>
                <div className="trip-tile__foot">
                  <AvatarStack
                    people={tr.membersPreview.map((p) => ({ name: p.displayName, photoUrl: p.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }))}
                  />
                  <span className="screen__row">
                    {tr.pendingRequests > 0 && <Chip tone="accent">{t('home.requestCount', { count: tr.pendingRequests })}</Chip>}
                    <Chip>{t('home.planCount', { count: tr.planCount })}</Chip>
                  </span>
                </div>
              </CardBody>
            </Link>
          </Card>
        ))}
        <div className="home__actions">
          <button type="button" className="home__action home__action--dashed" onClick={() => setCreating(true)}>
            <Icon name="plus" size={18} />
            {t('home.newTrip')}
          </button>
          <button type="button" className="home__action" onClick={() => setJoining(true)}>
            {t('home.joinCode')}
          </button>
        </div>
      </section>

      {nearby.length > 0 && (
        <section className="screen__section" aria-labelledby="home-matches">
          <div className="screen__section-head">
            <h2 id="home-matches" className="screen__section-title">
              {t('home.matches')}
            </h2>
            <Link to="/discover" className="screen__link">
              {t('common.seeAll')}
            </Link>
          </div>
          <ul className="list-reset home__carousel">
            {nearby.map((m) => (
              <PlanTile
                key={m.plan.id}
                compact
                plan={m.plan}
                origin={origin}
                distance={m.distance.underOneKm ? t('plan.underOneKm') : t('plan.km', { count: m.distance.km })}
              />
            ))}
          </ul>
        </section>
      )}
      <CreateSheet open={creating} onClose={() => setCreating(false)} initialMode="trip" />
      <BottomSheet
        open={joining}
        onClose={() => setJoining(false)}
        title={t('home.joinCode')}
        footer={
          <Button size="lg" block type="submit" form="join-code" disabled={!code.trim()}>
            {t('home.openTrip')}
          </Button>
        }
      >
        <form id="join-code" onSubmit={join}>
          <TextField label={t('home.codeLabel')} value={code} onChange={(e) => setCode(e.target.value)} hint={t('home.codeHint')} />
        </form>
      </BottomSheet>
    </div>
  )
}
