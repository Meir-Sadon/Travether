import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { BottomSheet, Button, Chip, Icon, IconButton, Segmented, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import type { Card } from '../lib/types'
import type { CategoryKey } from '../mock/data'
import { CardForm, type CardFields } from './CardForm'
import './CreateSheet.css'

type Mode = 'choose' | 'plan' | 'trip'

const categories: CategoryKey[] = ['hike', 'dayTrip', 'food', 'nightlife', 'tour', 'beach', 'transport', 'other']

/** The + button: create a Vacation Card or an Activity Plan (bottom-sheet forms, PLAN.md §5). */
export function CreateSheet({ open, onClose, initialMode = 'choose' }: { open: boolean; onClose: () => void; initialMode?: Mode }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [mode, setMode] = useState<Mode>(initialMode)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const close = () => {
    onClose()
    setMode(initialMode)
    setError(null)
  }

  const createCard = async (fields: CardFields) => {
    setBusy(true)
    setError(null)
    try {
      const card = await api.post<Card>('/cards', fields)
      finish(`/trips/${card.id}`)
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }
  const finish = (to: string) => {
    close()
    void navigate(to)
  }

  const title = mode === 'plan' ? t('create.planTitle') : mode === 'trip' ? t('create.tripTitle') : t('create.title')

  return (
    <BottomSheet
      open={open}
      onClose={close}
      title={title}
      footer={
        mode === 'plan' ? (
          <Button size="lg" block onClick={() => finish('/trips/cm-crew')}>
            {t('create.publishPlan')}
          </Button>
        ) : mode === 'trip' ? (
          <Button size="lg" block type="submit" form="create-card" loading={busy}>
            {t('create.createTrip')}
          </Button>
        ) : undefined
      }
    >
      {mode === 'choose' && (
        <ul className="list-reset create__choices">
          <li>
            <button type="button" className="create__choice" onClick={() => setMode('plan')}>
              <span className="create__choice-icon">
                <Icon name="calendar" />
              </span>
              <span>
                <strong>{t('create.planTitle')}</strong>
                <span className="screen__meta">{t('create.planHint')}</span>
              </span>
            </button>
          </li>
          <li>
            <button type="button" className="create__choice" onClick={() => setMode('trip')}>
              <span className="create__choice-icon">
                <Icon name="users" />
              </span>
              <span>
                <strong>{t('create.tripTitle')}</strong>
                <span className="screen__meta">{t('create.tripHint')}</span>
              </span>
            </button>
          </li>
        </ul>
      )}
      {mode === 'plan' && <PlanForm />}
      {mode === 'trip' && <CardForm id="create-card" error={error} onSubmit={(f) => void createCard(f)} />}
    </BottomSheet>
  )
}

function PlanForm() {
  const { t } = useTranslation()
  const [category, setCategory] = useState<CategoryKey>('hike')
  const [precision, setPrecision] = useState<'exact' | 'regional'>('exact')
  const [seats, setSeats] = useState(8)
  const [audience, setAudience] = useState<'open' | 'groupsOnly'>('open')

  return (
    <form className="screen__stack" onSubmit={(e) => e.preventDefault()}>
      <p className="screen__meta">{t('create.inTrip', { name: 'Chiang Mai Crew' })}</p>
      <fieldset className="create__fieldset">
        <legend className="field__label">{t('create.type')}</legend>
        <div className="screen__row">
          {categories.map((c) => (
            <Chip key={c} size="md" selected={category === c} onToggle={() => setCategory(c)}>
              {t(`category.${c}`)}
            </Chip>
          ))}
        </div>
      </fieldset>
      <TextField label={t('create.fieldTitle')} defaultValue="Sunrise hike to Doi Suthep" />
      <div className="create__two">
        <TextField label={t('create.date')} type="date" defaultValue="2026-10-14" />
        <TextField label={t('create.time')} type="time" defaultValue="05:30" />
      </div>
      <TextField label={t('create.meetingPoint')} defaultValue="Monk’s Trail trailhead" hint={t('create.meetingPointHint')} />
      <Segmented
        label={t('create.precision')}
        value={precision}
        onChange={setPrecision}
        options={[
          { value: 'exact', label: t('create.exact') },
          { value: 'regional', label: t('create.regional') },
        ]}
      />
      <TextField label={t('create.destination')} defaultValue="Wat Phra That Doi Suthep" />
      <div className="create__seats">
        <span>
          <strong>{t('create.seats')}</strong>
          <span className="screen__meta">{t('create.seatsHint', { count: 3 })}</span>
        </span>
        <span className="screen__row">
          <IconButton icon="minus" label={t('create.fewerSeats')} onClick={() => setSeats((s) => Math.max(2, s - 1))} />
          <output aria-live="polite" className="create__seat-count">
            {seats}
          </output>
          <IconButton icon="plus" label={t('create.moreSeats')} onClick={() => setSeats((s) => Math.min(30, s + 1))} />
        </span>
      </div>
      <fieldset className="create__fieldset">
        <legend className="field__label">{t('create.audience')}</legend>
        {(['open', 'groupsOnly'] as const).map((a) => (
          // The label's text comes from t(), which the linter can't see.
          // oxlint-disable-next-line jsx-a11y/label-has-associated-control
          <label key={a} className="create__radio">
            <input type="radio" name="audience" checked={audience === a} onChange={() => setAudience(a)} />
            <span>
              <strong>{t(`create.audience_${a}`)}</strong>
              <span className="screen__meta">{t(`create.audience_${a}_hint`)}</span>
            </span>
          </label>
        ))}
      </fieldset>
      <TextField multiline label={t('create.purpose')} defaultValue="Monk’s trail up, sunrise at the temple, breakfast after." />
    </form>
  )
}
