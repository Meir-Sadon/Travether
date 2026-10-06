import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { AvatarStack, Card, CardBody, Chip, Icon } from '../components'
import { CreateSheet } from '../features/CreateSheet'
import { PlanCard } from '../features/PlanCard'
import { ScreenHeader } from '../layout/ScreenHeader'
import { me, myTripIds, person, plans, trip } from '../mock/data'
import './HomeScreen.css'

/** 3 · Home: my trips + plans matching my dates nearby. */
export function HomeScreen() {
  const { t } = useTranslation()
  const [creating, setCreating] = useState(false)
  const myTrips = myTripIds.map(trip).filter((x) => x !== undefined)
  const nearby = plans.filter((p) => !myTripIds.includes(p.tripId))

  return (
    <div className="screen">
      <ScreenHeader large title={t('home.greeting', { name: me.name })} subtitle={t('home.subtitle')} />

      <section className="screen__section" aria-labelledby="home-trips">
        <h2 id="home-trips" className="screen__section-title">
          {t('home.myTrips')}
        </h2>
        {myTrips.map((tr) => (
          <Card key={tr.id} interactive className="trip-tile">
            <Link to={`/trips/${tr.id}`} className="trip-tile__link">
              <div className="trip-tile__cover" style={{ background: tr.tint }}>
                <Chip tone="inverse">
                  {tr.country} · {tr.dates}
                </Chip>
              </div>
              <CardBody>
                <strong className="trip-tile__name">{tr.name}</strong>
                <span className="screen__meta">{tr.regions.join(' · ')}</span>
                <div className="trip-tile__foot">
                  <AvatarStack people={tr.members.map((m) => person(m.id))} />
                  <span className="screen__row">
                    <Chip>{t('home.planCount', { count: plans.filter((p) => p.tripId === tr.id).length })}</Chip>
                    {tr.pendingRequests > 0 && <Chip tone="accent">{t('home.requestCount', { count: tr.pendingRequests })}</Chip>}
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
          <Link to="/c/cm-crew-7K2" className="home__action">
            {t('home.joinCode')}
          </Link>
        </div>
      </section>

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
          {nearby.map((p) => (
            <PlanCard key={p.id} plan={p} variant="compact" />
          ))}
        </ul>
      </section>
      <CreateSheet open={creating} onClose={() => setCreating(false)} initialMode="trip" />
    </div>
  )
}
