import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useParams } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, Card, CardBody, Chip, FormError, Icon } from '../components'
import { PersonItem } from '../features/PersonItem'
import { PlanForm, type PlanFields } from '../features/PlanForm'
import { api, errorCode } from '../lib/api'
import { categoryTint, formatPlanWhen, mapLink } from '../lib/plans'
import type { Card as TripCard, Plan } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './PlanScreen.css'

/** 6 · Activity Plan: what, when, where (privately), who's going (PLAN.md §4.3). */
export function PlanScreen() {
  const { t, i18n } = useTranslation()
  const { planId } = useParams()
  const { user } = useAuth()
  const here = useLocation()
  const { data: plan, error, setData } = useApi<Plan>(user === undefined ? null : `/plans/${planId}`)
  const [editing, setEditing] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const { data: card } = useApi<TripCard>(editing && plan ? `/cards/${plan.cardId}` : null)

  if (error === 'NotFound') return <NotFoundScreen />
  if (!plan) {
    return (
      <p className="screen__loading" role="status">
        {error ? <FormError code={error} /> : t('common.loading')}
      </p>
    )
  }

  const run = async (work: () => Promise<void>) => {
    setBusy(true)
    setActionError(null)
    try {
      await work()
    } catch (err) {
      setActionError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  const save = (fields: PlanFields) =>
    run(async () => {
      setData(await api.patch<Plan>(`/plans/${plan.id}`, fields))
      setEditing(false)
    })

  const going = plan.access !== 'public'
  const left = plan.seatLimit - plan.seatsTaken
  const closed = plan.status === 'cancelled' || plan.status === 'done'
  const insider = plan.participants !== null
  const backTo = insider ? `/trips/${plan.cardId}` : '/discover'

  return (
    <div className="screen plan" style={{ minBlockSize: '100dvh' }}>
      <div className="plan__cover" style={{ background: categoryTint[plan.category] }}>
        <Link to={backTo} className="icon-btn icon-btn--raised plan__back" aria-label={t('common.back')}>
          <Icon name="back" />
        </Link>
      </div>

      <section className="screen__section">
        <span className="screen__row">
          <Chip tint={categoryTint[plan.category]}>{t(`category.${plan.category}`)}</Chip>
          {plan.status === 'cancelled' && <Chip>{t('plan.cancelled')}</Chip>}
          {plan.status === 'full' && <Chip>{t('plan.full')}</Chip>}
          {plan.access === 'participant' && <Chip tone="accent">{t('plan.youreGoing')}</Chip>}
        </span>
        <h1 className="plan__title">{plan.title}</h1>
        <ul className="list-reset plan__facts">
          <li>
            <Icon name="calendar" size={18} />
            <span>
              {formatPlanWhen(plan.localDate, plan.localTime, i18n.language)}
              <span className="screen__meta">{t('plan.localTime', { zone: plan.timeZoneId })}</span>
            </span>
          </li>
          <li>
            <Icon name="pin" size={18} />
            {plan.meetingPoint ? (
              <span>
                <strong>{t('plan.meet', { place: plan.meetingPoint.name })}</strong>
                <span className="screen__meta">{plan.areaLabel}</span>
                <a href={mapLink(plan.meetingPoint.lat, plan.meetingPoint.lng)} target="_blank" rel="noreferrer">
                  {t('plan.openMap')}
                </a>
              </span>
            ) : (
              <span>
                {plan.distance
                  ? t('plan.area', {
                      area: plan.areaLabel,
                      distance: plan.distance.underOneKm ? t('plan.underOneKm') : t('plan.km', { count: plan.distance.km }),
                    })
                  : plan.areaLabel}
                <span className="screen__meta plan__private">{t('plan.exactAfterApproval')}</span>
              </span>
            )}
          </li>
          <li>
            <Icon name="map" size={18} />
            <span>{plan.destination ? t('plan.destination', { place: plan.destination }) : t('plan.destinationPrivate')}</span>
          </li>
          <li>
            <Icon name="users" size={18} />
            {t('plan.going', { going: plan.seatsTaken, left: Math.max(0, left) })} · {t(`plan.audience_${plan.audience}`)}
          </li>
        </ul>
        {plan.purpose && <p className="screen__body plan__purpose">{plan.purpose}</p>}
        {plan.canManage && !closed && (
          <div className="screen__row">
            <Button size="sm" variant="secondary" onClick={() => setEditing(true)}>
              {t('create.editPlan')}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => setConfirmCancel(true)}>
              {t('plan.cancel')}
            </Button>
          </div>
        )}
        <FormError code={editing || confirmCancel ? null : actionError} />
      </section>

      <section className="screen__section" aria-labelledby="plan-hosts">
        <h2 id="plan-hosts" className="screen__section-title">
          {plan.cardName ? t('plan.hostedBy', { group: plan.cardName }) : t('plan.host')}
        </h2>
        <PersonItem person={plan.host} you={plan.host.id === user?.id} />
      </section>

      {plan.participants && plan.participants.length > 1 && (
        <section className="screen__section" aria-labelledby="plan-going">
          <h2 id="plan-going" className="screen__section-title">
            {t('plan.whoIsGoing')}
          </h2>
          <ul className="list-reset screen__stack">
            {plan.participants
              .filter((p) => p.id !== plan.host.id)
              .map((p) => (
                <li key={p.id}>
                  <PersonItem person={p} you={p.id === user?.id} />
                </li>
              ))}
          </ul>
        </section>
      )}

      <section className="screen__section">
        <Card variant="filled">
          <CardBody>
            <strong>
              <Icon name="shieldCheck" size={16} className="plan__inline-icon" /> {t('plan.safetyTitle')}
            </strong>
            <span className="screen__meta">{t('plan.safetyBody')}</span>
          </CardBody>
        </Card>
      </section>

      {!closed && (
        <footer className="screen__footer">
          {!user && (
            <Link to={`/signup?next=${encodeURIComponent(here.pathname)}`} className="btn btn--primary btn--lg btn--block">
              {t('plan.signUpToJoin')}
            </Link>
          )}
          {plan.canSelfJoin && (
            <Button size="lg" block loading={busy} onClick={() => void run(async () => setData(await api.post<Plan>(`/plans/${plan.id}/join`)))}>
              {t('plan.join')}
            </Button>
          )}
          {going && (
            <Link to={`/inbox/plan-${plan.id}`} className="btn btn--primary btn--lg btn--block">
              <Icon name="chat" />
              {t('plan.openChat')}
            </Link>
          )}
          {plan.access === 'participant' && (
            <Button block variant="ghost" loading={busy} onClick={() => void run(async () => setData(await api.post<Plan>(`/plans/${plan.id}/leave`)))}>
              {t('plan.leave')}
            </Button>
          )}
        </footer>
      )}

      <BottomSheet
        open={editing}
        onClose={() => setEditing(false)}
        title={t('create.editPlan')}
        footer={
          <Button size="lg" block type="submit" form="edit-plan" loading={busy} disabled={!card}>
            {t('profile.save')}
          </Button>
        }
      >
        {card && user && <PlanForm id="edit-plan" card={card} meId={user.id} initial={plan} error={actionError} onSubmit={(f) => void save(f)} />}
      </BottomSheet>

      <BottomSheet
        open={confirmCancel}
        onClose={() => setConfirmCancel(false)}
        title={t('plan.cancelTitle')}
        footer={
          <div className="screen__stack">
            <Button block size="lg" variant="brand" onClick={() => setConfirmCancel(false)}>
              {t('plan.keep')}
            </Button>
            <Button
              block
              variant="ghost"
              loading={busy}
              onClick={() =>
                void run(async () => {
                  setData(await api.post<Plan>(`/plans/${plan.id}/cancel`))
                  setConfirmCancel(false)
                })
              }
            >
              {t('plan.cancelConfirm')}
            </Button>
          </div>
        }
      >
        <p className="screen__body">{t('plan.cancelBody')}</p>
        <FormError code={actionError} />
      </BottomSheet>
    </div>
  )
}
