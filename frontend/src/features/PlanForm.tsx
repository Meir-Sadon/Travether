import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Checkbox, Chip, FormError, IconButton, Segmented, TextField } from '../components'
import { planCategories } from '../lib/plans'
import type { Card, LatLng, LocationPrecision, Place, Plan, PlanAudience, PlanCategory } from '../lib/types'
import { PlaceField } from './PlaceField'
import './CreateSheet.css'

export type PlanFields = {
  title: string
  category: PlanCategory
  /** Left out on edit when the meeting point wasn't touched. */
  origin?: LatLng
  originName?: string
  originAreaLabel?: string
  destination: string
  destinationPrecision: LocationPrecision
  date: string
  time: string
  purpose: string
  seatLimit: number
  audience: PlanAudience
  participantIds?: string[]
}

/** Templates (PLAN.md §4.3): each category suggests a group size, and a title and description (as placeholders). */
const templateSeats: Record<PlanCategory, number> = { hike: 6, dayTrip: 6, food: 6, nightlife: 8, tour: 8, beach: 8, transport: 4, other: 6 }

type PlanFormProps = {
  id: string
  card: Card
  meId: string
  initial?: Plan
  error: string | null
  onSubmit: (fields: PlanFields) => void
}

/**
 * What, where, when, seats and audience. Shared by "New Activity Plan" and "Edit plan".
 * Submitted by a button elsewhere (a sheet footer) through the form's `id`.
 */
export function PlanForm({ id, card, meId, initial, error, onSubmit }: PlanFormProps) {
  const { t } = useTranslation()
  const [category, setCategory] = useState<PlanCategory>(initial?.category ?? 'hike')
  const [title, setTitle] = useState(initial?.title ?? '')
  const [date, setDate] = useState(initial?.localDate ?? '')
  const [time, setTime] = useState(initial?.localTime.slice(0, 5) ?? '')
  const [place, setPlace] = useState<Place | null>(
    initial?.meetingPoint ? { name: initial.meetingPoint.name, area: initial.areaLabel, lat: initial.meetingPoint.lat, lng: initial.meetingPoint.lng } : null,
  )
  const [placeTouched, setPlaceTouched] = useState(false)
  const [destination, setDestination] = useState(initial?.destination ?? '')
  const [precision, setPrecision] = useState<LocationPrecision>(initial?.destinationPrecision ?? 'exact')
  const [seats, setSeats] = useState(initial?.seatLimit ?? templateSeats.hike)
  const [seatsTouched, setSeatsTouched] = useState(!!initial)
  const [audience, setAudience] = useState<PlanAudience>(initial?.audience ?? 'open')
  const [purpose, setPurpose] = useState(initial?.purpose ?? '')
  const [bringing, setBringing] = useState<string[]>([])
  const [localError, setLocalError] = useState<string | null>(null)
  const others = (card.members ?? []).filter((m) => m.person.id !== meId)
  const going = initial ? initial.seatsTaken : bringing.length + 1

  const pickCategory = (c: PlanCategory) => {
    setCategory(c)
    if (!seatsTouched) setSeats(Math.max(templateSeats[c], going))
  }

  const changeSeats = (n: number) => {
    setSeatsTouched(true)
    setSeats(Math.min(100, Math.max(2, n)))
  }

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const keepPlace = initial && !placeTouched
    if (!keepPlace && !place) return setLocalError('MeetingPointRequired')
    setLocalError(null)
    onSubmit({
      title: title.trim(),
      category,
      ...(keepPlace || !place ? {} : { origin: { lat: place.lat, lng: place.lng }, originName: place.name, originAreaLabel: place.area }),
      destination: destination.trim(),
      destinationPrecision: precision,
      date,
      time,
      purpose,
      seatLimit: seats,
      audience,
      ...(initial ? {} : { participantIds: bringing }),
    })
  }

  return (
    <form id={id} className="screen__stack" onSubmit={submit} noValidate>
      <p className="screen__meta">{t('create.inTrip', { name: card.name })}</p>
      <fieldset className="create__fieldset">
        <legend className="field__label">{t('create.type')}</legend>
        <div className="screen__row">
          {planCategories.map((c) => (
            <Chip key={c} size="md" selected={category === c} onToggle={() => pickCategory(c)}>
              {t(`category.${c}`)}
            </Chip>
          ))}
        </div>
      </fieldset>
      <TextField label={t('create.fieldTitle')} required maxLength={80} placeholder={t(`create.template_${category}_title`)} value={title} onChange={(e) => setTitle(e.target.value)} />
      <div className="create__two">
        <TextField label={t('create.date')} type="date" required min={card.startsOn} max={card.endsOn} value={date} onChange={(e) => setDate(e.target.value)} />
        <TextField label={t('create.time')} type="time" required value={time} onChange={(e) => setTime(e.target.value)} />
      </div>
      <PlaceField
        label={t('create.meetingPoint')}
        hint={initial && !place ? t('create.meetingPointKept', { area: initial.areaLabel }) : t('create.meetingPointHint')}
        value={place}
        onChange={(p) => {
          setPlace(p)
          setPlaceTouched(true)
        }}
      />
      <TextField label={t('create.destination')} required maxLength={200} value={destination} onChange={(e) => setDestination(e.target.value)} />
      <Segmented
        label={t('create.precision')}
        value={precision}
        onChange={setPrecision}
        options={[
          { value: 'exact', label: t('create.exact') },
          { value: 'regional', label: t('create.regional') },
        ]}
      />
      <p className="screen__meta">{precision === 'exact' ? t('create.exactHint') : t('create.regionalHint')}</p>
      {!initial && others.length > 0 && (
        <fieldset className="create__fieldset">
          <legend className="field__label">{t('create.bring')}</legend>
          {others.map((m) => (
            <Checkbox
              key={m.person.id}
              checked={bringing.includes(m.person.id)}
              onChange={(e) => {
                const next = e.target.checked ? [...bringing, m.person.id] : bringing.filter((x) => x !== m.person.id)
                setBringing(next)
                if (seats < next.length + 1) setSeats(next.length + 1)
              }}
            >
              {m.person.displayName}
            </Checkbox>
          ))}
        </fieldset>
      )}
      <div className="create__seats">
        <span>
          <strong>{t('create.seats')}</strong>
          <span className="screen__meta">{t('create.seatsHint', { count: going })}</span>
        </span>
        <span className="screen__row">
          <IconButton icon="minus" label={t('create.fewerSeats')} onClick={() => changeSeats(Math.max(seats - 1, going))} />
          <output aria-live="polite" className="create__seat-count">
            {seats}
          </output>
          <IconButton icon="plus" label={t('create.moreSeats')} onClick={() => changeSeats(seats + 1)} />
        </span>
      </div>
      <fieldset className="create__fieldset">
        <legend className="field__label">{t('create.audience')}</legend>
        {(['open', 'groupsOnly'] as const).map((a) => (
          // The label's text comes from t(), which the linter can't see.
          // oxlint-disable-next-line jsx-a11y/label-has-associated-control
          <label key={a} className="create__radio">
            <input type="radio" name={`${id}-audience`} checked={audience === a} onChange={() => setAudience(a)} />
            <span>
              <strong>{t(`create.audience_${a}`)}</strong>
              <span className="screen__meta">{t(`create.audience_${a}_hint`)}</span>
            </span>
          </label>
        ))}
      </fieldset>
      <TextField multiline label={t('create.purpose')} maxLength={1000} placeholder={t(`create.template_${category}_purpose`)} value={purpose} onChange={(e) => setPurpose(e.target.value)} />
      <FormError code={localError ?? error} />
    </form>
  )
}
