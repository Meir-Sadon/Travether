import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useMe } from '../auth/useAuth'
import { BottomSheet, Button, Icon, SelectField } from '../components'
import { api, errorCode } from '../lib/api'
import { track } from '../lib/telemetry'
import { todayIso } from '../lib/dates'
import type { Card, MyCard, Plan } from '../lib/types'
import { useApi } from '../lib/useApi'
import { CardForm, type CardFields } from './CardForm'
import { PlanForm, type PlanFields } from './PlanForm'
import './CreateSheet.css'

type Mode = 'choose' | 'plan' | 'trip'

/** The + button: create a Vacation Card or an Activity Plan (bottom-sheet forms, PLAN.md §5). */
type CreateSheetProps = {
  open: boolean
  onClose: () => void
  initialMode?: Mode
  /** The trip a new plan belongs to; when absent the sheet asks. */
  cardId?: string
}

export function CreateSheet({ open, onClose, initialMode = 'choose', cardId }: CreateSheetProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [mode, setMode] = useState<Mode>(initialMode)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [planCardId, setPlanCardId] = useState(cardId ?? '')

  const close = () => {
    onClose()
    setMode(initialMode)
    setPlanCardId(cardId ?? '')
    setError(null)
  }

  const createCard = async (fields: CardFields) => {
    setBusy(true)
    setError(null)
    try {
      const card = await api.post<Card>('/cards', fields)
      track('card_created', { visibility: card.visibility })
      finish(`/trips/${card.id}`)
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }
  const createPlan = async (fields: PlanFields) => {
    setBusy(true)
    setError(null)
    try {
      const plan = await api.post<Plan>(`/cards/${planCardId}/plans`, fields)
      track('plan_created', { category: plan.category, audience: plan.audience })
      finish(`/plans/${plan.id}`)
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
          <Button size="lg" block type="submit" form="create-plan" loading={busy} disabled={!planCardId}>
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
      {mode === 'plan' && (
        <PlanCreate cardId={planCardId} fixed={!!cardId} onPickCard={setPlanCardId} error={error} onSubmit={(f) => void createPlan(f)} />
      )}
      {mode === 'trip' && <CardForm id="create-card" error={error} onSubmit={(f) => void createCard(f)} />}
    </BottomSheet>
  )
}

type PlanCreateProps = {
  cardId: string
  /** The sheet was opened from a trip: no trip picker. */
  fixed: boolean
  onPickCard: (id: string) => void
  error: string | null
  onSubmit: (fields: PlanFields) => void
}

function PlanCreate({ cardId, fixed, onPickCard, error, onSubmit }: PlanCreateProps) {
  const { t } = useTranslation()
  const me = useMe()
  const { data: trips } = useApi<MyCard[]>(fixed ? null : '/cards')
  const { data: card } = useApi<Card>(cardId ? `/cards/${cardId}` : null)
  const today = todayIso()
  const current = trips?.filter((c) => c.endsOn >= today) ?? []

  return (
    <div className="screen__stack">
      {!fixed && trips && current.length === 0 && <p className="screen__empty">{t('create.noTripsForPlan')}</p>}
      {!fixed && current.length > 0 && (
        <SelectField
          label={t('create.chooseTrip')}
          placeholder={t('create.chooseTrip')}
          value={cardId}
          onChange={(e) => onPickCard(e.target.value)}
          options={current.map((c) => ({ value: c.id, label: c.name }))}
        />
      )}
      {card && card.id === cardId && <PlanForm key={card.id} id="create-plan" card={card} meId={me.id} error={error} onSubmit={onSubmit} />}
    </div>
  )
}
