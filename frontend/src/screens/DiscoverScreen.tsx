import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, Chip, FormError, IconButton, Segmented, TextField } from '../components'
import { PlaceField } from '../features/PlaceField'
import { PlanTile } from '../features/PlanTile'
import { ScreenHeader } from '../layout/ScreenHeader'
import { formatDateRange, todayIso } from '../lib/dates'
import { discoverPath, rememberOrigin, useTripOrigin } from '../lib/discover'
import { planCategories } from '../lib/plans'
import type { DiscoverPlan, MyCard, Place, PlanCategory } from '../lib/types'
import { useApi } from '../lib/useApi'
import './DiscoverScreen.css'

const sizes = [undefined, 4, 8] as const

function addDays(iso: string, days: number): string {
  const d = new Date(`${iso}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** 5 · Discover: open plans near you on your trip's dates, nearest first (PLAN.md §4.4). */
export function DiscoverScreen() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const today = todayIso()
  const { data: myTrips } = useApi<MyCard[]>(user ? '/cards' : null)
  const trips = myTrips?.filter((c) => c.endsOn >= today) ?? []
  const [tripId, setTripId] = useState<string | null>(null)
  const trip = trips.find((c) => c.id === tripId) ?? trips[0] ?? null
  const tripOrigin = useTripOrigin(trip, i18n.language)
  const [picked, setPicked] = useState<{
    tripId: string | null
    place: Place
  } | null>(null)
  const origin = picked && picked.tripId === (trip?.id ?? null) ? picked.place : tripOrigin

  const [customDates, setCustomDates] = useState<{
    from: string
    to: string
  } | null>(null)
  const from = customDates?.from ?? (trip ? (trip.startsOn > today ? trip.startsOn : today) : today)
  const to = customDates?.to ?? (trip ? trip.endsOn : addDays(today, 14))

  const [category, setCategory] = useState<PlanCategory | null>(null)
  const [radius, setRadius] = useState(30)
  const [maxSeats, setMaxSeats] = useState<number | undefined>(undefined)
  const [sort, setSort] = useState<'distance' | 'date'>('distance')
  const [filtersOpen, setFiltersOpen] = useState(false)
  const [placeOpen, setPlaceOpen] = useState(false)

  const query = origin
    ? discoverPath({
        origin,
        from,
        to,
        radiusKm: radius,
        category: category ?? undefined,
        maxSeats,
        sort,
      })
    : null
  const { data: found, error, loading } = useApi<DiscoverPlan[]>(user === undefined ? null : query)
  const results = found ?? []

  const pickTrip = (id: string) => {
    setTripId(id)
    setCustomDates(null)
  }

  const pickPlace = (place: Place | null) => {
    if (!place) return
    setPicked({ tripId: trip?.id ?? null, place })
    if (trip) rememberOrigin(trip, place)
    setPlaceOpen(false)
  }

  return (
    <div className="screen">
      <ScreenHeader
        large
        title={origin?.name ?? t('discover.title')}
        subtitle={t('discover.subtitle', {
          dates: formatDateRange(from, to, i18n.language),
          km: radius,
        })}
        action={<IconButton icon="filter" label={t('discover.filters')} variant="raised" onClick={() => setFiltersOpen(true)} />}
      />
      <div className="screen__section">
        {trips.length > 1 && (
          <div className="discover__chips" role="group" aria-label={t('discover.trips')}>
            {trips.map((c) => (
              <Chip key={c.id} size="md" selected={trip?.id === c.id} onToggle={() => pickTrip(c.id)}>
                {c.name}
              </Chip>
            ))}
          </div>
        )}
        <div className="screen__row">
          <Button size="sm" variant="secondary" icon="pin" onClick={() => setPlaceOpen(true)}>
            {origin ? t('discover.changePlace') : t('discover.pickPlace')}
          </Button>
        </div>
        <div className="discover__chips" role="group" aria-label={t('discover.categories')}>
          <Chip size="md" selected={category === null} onToggle={() => setCategory(null)}>
            {t('discover.all')}
          </Chip>
          {planCategories.map((c) => (
            <Chip key={c} size="md" selected={category === c} onToggle={() => setCategory(c)}>
              {t(`category.${c}`)}
            </Chip>
          ))}
        </div>
        {found && (
          <span className="screen__meta" role="status">
            {sort === 'distance' ? t('discover.count', { count: results.length }) : t('discover.countByDate', { count: results.length })}
          </span>
        )}
      </div>

      <ul className="list-reset screen__section screen__stack">
        {!origin && <li className="screen__empty">{user && trips.length === 0 ? t('discover.noTrip') : t('discover.noPlace')}</li>}
        {origin && loading && !found && (
          <li className="screen__loading" role="status">
            {t('common.loading')}
          </li>
        )}
        {error && (
          <li>
            <FormError code={error} />
          </li>
        )}
        {found && results.length === 0 && <li className="screen__empty">{t('discover.empty')}</li>}
        {results.map((r) => (
          <PlanTile
            key={r.plan.id}
            plan={r.plan}
            origin={origin}
            distance={r.distance.underOneKm ? t('plan.underOneKm') : t('plan.km', { count: r.distance.km })}
          />
        ))}
      </ul>

      <BottomSheet open={placeOpen} onClose={() => setPlaceOpen(false)} title={t('discover.whereFrom')}>
        <div className="screen__stack">
          <p className="screen__meta">{t('discover.whereFromHint')}</p>
          <PlaceField label={t('discover.place')} value={null} onChange={pickPlace} />
        </div>
      </BottomSheet>

      <BottomSheet
        open={filtersOpen}
        onClose={() => setFiltersOpen(false)}
        title={t('discover.filters')}
        footer={
          <Button block size="lg" onClick={() => setFiltersOpen(false)}>
            {found ? t('discover.showPlans', { count: results.length }) : t('common.done')}
          </Button>
        }
      >
        <div className="screen__stack">
          <Segmented
            label={t('discover.sort')}
            value={sort}
            onChange={setSort}
            options={[
              { value: 'distance', label: t('discover.nearest') },
              { value: 'date', label: t('discover.soonest') },
            ]}
          />
          <label className="field__label" htmlFor="radius">
            {t('discover.radius', { km: radius })}
          </label>
          <input id="radius" type="range" min={5} max={100} step={5} value={radius} onChange={(e) => setRadius(Number(e.target.value))} />
          <span className="field__label">{t('discover.groupSize')}</span>
          <div className="screen__row">
            {sizes.map((s) => (
              <Chip key={s ?? 'any'} size="md" selected={maxSeats === s} onToggle={() => setMaxSeats(s)}>
                {s ? t('discover.upTo', { count: s }) : t('discover.anySize')}
              </Chip>
            ))}
          </div>
          <div className="discover__dates">
            <TextField
              label={t('create.from')}
              type="date"
              value={from}
              min={today}
              onChange={(e) => e.target.value && setCustomDates({ from: e.target.value, to })}
            />
            <TextField
              label={t('create.to')}
              type="date"
              value={to}
              min={from}
              onChange={(e) => e.target.value && setCustomDates({ from, to: e.target.value })}
            />
          </div>
        </div>
      </BottomSheet>
    </div>
  )
}
