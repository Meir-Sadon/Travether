import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BottomSheet, Button, Checkbox, FormError, SelectField, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import { todayIso } from '../lib/dates'
import type { Card, MyCard, Plan } from '../lib/types'
import { useApi } from '../lib/useApi'
import './CreateSheet.css'

type PlanRequestSheetProps = {
  plan: Plan
  meId: string
  open: boolean
  onClose: () => void
  onSent: (plan: Plan) => void
}

/** Ask for a seat: alone, or with people from your own trip (required for "Groups only" plans). */
export function PlanRequestSheet({ plan, meId, open, onClose, onSent }: PlanRequestSheetProps) {
  const { t } = useTranslation()
  const groupsOnly = plan.audience === 'groupsOnly'
  const [withTrip, setWithTrip] = useState(groupsOnly)
  const [tripId, setTripId] = useState('')
  const [party, setParty] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const today = todayIso()
  const { data: myTrips } = useApi<MyCard[]>(open ? '/cards' : null)
  const trips = myTrips?.filter((c) => c.endsOn >= today) ?? []
  const chosen = tripId || trips[0]?.id || ''
  const { data: trip } = useApi<Card>(open && withTrip && chosen ? `/cards/${chosen}` : null)
  const others = trip?.id === chosen ? (trip.members ?? []).filter((m) => m.person.id !== meId) : []
  const seatsLeft = plan.seatLimit - plan.seatsTaken

  const send = async () => {
    setBusy(true)
    setError(null)
    try {
      const body = withTrip ? { message, sourceCardId: chosen, partyUserIds: party } : { message }
      onSent(await api.post<Plan>(`/plans/${plan.id}/requests`, body))
      onClose()
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <BottomSheet
      open={open}
      onClose={onClose}
      title={t('plan.request')}
      footer={
        <Button size="lg" block loading={busy} disabled={withTrip && !chosen} onClick={() => void send()}>
          {party.length > 0 ? t('plan.requestSeats', { count: party.length + 1 }) : t('plan.request')}
        </Button>
      }
    >
      <div className="screen__stack">
        <fieldset className="create__fieldset">
          <legend className="field__label">{t('plan.whoIsComing')}</legend>
          {(['me', 'trip'] as const).map((who) => (
            // The label's text comes from t(), which the linter can't see.
            // oxlint-disable-next-line jsx-a11y/label-has-associated-control
            <label key={who} className="create__radio">
              <input
                type="radio"
                name="party"
                checked={withTrip === (who === 'trip')}
                disabled={who === 'me' && groupsOnly}
                onChange={() => {
                  setWithTrip(who === 'trip')
                  setParty([])
                }}
              />
              <span>
                <strong>{t(`plan.party_${who}`)}</strong>
                {who === 'me' && groupsOnly && <span className="screen__meta">{t('plan.groupsOnlyHint')}</span>}
              </span>
            </label>
          ))}
        </fieldset>
        {withTrip && myTrips && trips.length === 0 && <p className="screen__note">{t('plan.needTrip')}</p>}
        {withTrip && trips.length > 0 && (
          <SelectField
            label={t('plan.fromTrip')}
            value={chosen}
            onChange={(e) => {
              setTripId(e.target.value)
              setParty([])
            }}
            options={trips.map((c) => ({ value: c.id, label: c.name }))}
          />
        )}
        {withTrip && others.length > 0 && (
          <fieldset className="create__fieldset">
            <legend className="field__label">{t('plan.bringing', { count: seatsLeft - 1 })}</legend>
            {others.map((m) => (
              <Checkbox
                key={m.person.id}
                checked={party.includes(m.person.id)}
                disabled={!party.includes(m.person.id) && party.length + 1 >= seatsLeft}
                onChange={(e) => setParty(e.target.checked ? [...party, m.person.id] : party.filter((x) => x !== m.person.id))}
              >
                {m.person.displayName}
              </Checkbox>
            ))}
          </fieldset>
        )}
        <TextField
          multiline
          label={t('plan.message')}
          hint={t('plan.messageHint')}
          maxLength={300}
          rows={3}
          value={message}
          onChange={(e) => setMessage(e.target.value)}
        />
        <FormError code={error} />
      </div>
    </BottomSheet>
  )
}
