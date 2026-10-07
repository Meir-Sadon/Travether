import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useParams, useSearchParams } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { BottomSheet, Button, Card, CardBody, Chip, FormError, Icon, Stepper } from '../components'
import { PersonItem } from '../features/PersonItem'
import { PlanForm, type PlanFields } from '../features/PlanForm'
import { PlanRequestSheet } from '../features/PlanRequestSheet'
import { ReportSheet, type ReportTarget } from '../features/ReportSheet'
import { api, errorCode } from '../lib/api'
import { categoryTint, formatPlanWhen, mapLink } from '../lib/plans'
import { planShareText, whatsAppLink } from '../lib/share'
import type { Card as TripCard, Plan, PlanRequest } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './PlanScreen.css'

/** 6 · Activity Plan: what, when, where (privately), who's going (PLAN.md §4.3). */
export function PlanScreen() {
  const { t, i18n } = useTranslation()
  const { planId } = useParams()
  const { user } = useAuth()
  const here = useLocation()
  // Opened from Discover: the search origin, so the page shows the same rounded distance.
  const [search] = useSearchParams()
  const near = search.get('lat') && search.get('lng') ? `?lat=${search.get('lat')}&lng=${search.get('lng')}` : ''
  const { data: plan, error, setData } = useApi<Plan>(user === undefined ? null : `/plans/${planId}${near}`)
  const [editing, setEditing] = useState(false)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [requesting, setRequesting] = useState(false)
  const [reporting, setReporting] = useState<ReportTarget | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const { data: card } = useApi<TripCard>(editing && plan ? `/cards/${plan.cardId}` : null)
  const requests = useApi<PlanRequest[]>(plan?.canManage && plan.pendingRequestCount ? `/plans/${planId}/requests` : null)

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

  const decide = (request: PlanRequest, verdict: 'approve' | 'reject') =>
    run(async () => {
      setData(await api.post<Plan>(`/plan-requests/${request.id}/${verdict}`))
      requests.reload()
    })

  const going = plan.access !== 'public'
  const insider = plan.participants !== null
  const request = plan.myRequest
  const pending = request?.status === 'requested'
  // Outsiders (not in the plan's trip) ask for a seat; trip members join directly.
  const canAsk = !!user && !insider && !pending && plan.status === 'open'
  const left = plan.seatLimit - plan.seatsTaken
  const closed = plan.status === 'cancelled' || plan.status === 'done'
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
        {!closed && <PlanShareRow plan={plan} />}
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

      {plan.canManage && !!requests.data?.length && (
        <section className="screen__section" aria-labelledby="plan-requests">
          <h2 id="plan-requests" className="screen__section-title">
            {t('trip.requests', { count: requests.data.length })}
          </h2>
          <ul className="list-reset screen__stack">
            {requests.data.map((r) => (
              <li key={r.id} className="screen__stack">
                <PersonItem
                  person={r.requester}
                  trailing={r.party.length > 0 ? <Chip>{t('plan.partySize', { count: r.party.length + 1 })}</Chip> : undefined}
                />
                {r.party.length > 0 && (
                  <p className="screen__meta">
                    {t('plan.withParty', { names: r.party.map((p) => p.displayName).join(', '), trip: r.sourceCardName ?? '' })}
                  </p>
                )}
                {r.message && <p className="screen__body">{r.message}</p>}
                <div className="screen__row">
                  <Button size="sm" loading={busy} onClick={() => void decide(r, 'approve')}>
                    {t('trip.approve')}
                  </Button>
                  <Button size="sm" variant="secondary" disabled={busy} onClick={() => void decide(r, 'reject')}>
                    {t('trip.decline')}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        </section>
      )}

      {request && request.status !== 'approved' && !going && (
        <section className="screen__section">
          <Card>
            <CardBody>
              <strong>{t('plan.yourRequest')}</strong>
              {pending ? (
                <>
                  <div className="plan__stepper">
                    <Stepper label={t('plan.requestStatus')} steps={[t('plan.stepRequested'), t('plan.stepApproved'), t('plan.stepChat')]} current={1} />
                  </div>
                  <p className="screen__meta plan__center">{t('plan.waiting', { hosts: plan.host.displayName })}</p>
                </>
              ) : (
                <p className="screen__meta">
                  {request.status === 'rejected' ? t('plan.request_rejected') : request.status === 'expired' ? t('plan.request_expired') : t('plan.request_withdrawn')}
                </p>
              )}
            </CardBody>
          </Card>
        </section>
      )}

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
                  <PersonItem
                    person={p}
                    you={p.id === user?.id}
                    trailing={
                      plan.canManage && !closed ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          aria-label={t('plan.removeName', { name: p.displayName })}
                          disabled={busy}
                          onClick={() => void run(async () => setData(await api.del<Plan>(`/plans/${plan.id}/participants/${p.id}`)))}
                        >
                          {t('plan.remove')}
                        </Button>
                      ) : undefined
                    }
                  />
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
            <Link to="/safety" className="screen__link">
              {t('plan.safetyMore')}
            </Link>
          </CardBody>
        </Card>
        {user && plan.access !== 'host' && (
          <Button variant="ghost" size="sm" onClick={() => setReporting({ type: 'plan', id: plan.id })}>
            {t('report.title_plan')}
          </Button>
        )}
      </section>
      <ReportSheet target={reporting} onClose={() => setReporting(null)} />

      {!closed && (
        <footer className="screen__footer">
          {!user && (
            <Link to={`/signup?next=${encodeURIComponent(here.pathname)}`} className="btn btn--primary btn--lg btn--block">
              {t('plan.signUpToJoin')}
            </Link>
          )}
          {canAsk && (
            <Button size="lg" block onClick={() => setRequesting(true)}>
              {request?.status === 'rejected' || request?.status === 'expired' ? t('plan.requestAgain') : t('plan.request')}
            </Button>
          )}
          {pending && (
            <Button
              variant="secondary"
              size="lg"
              block
              loading={busy}
              onClick={() =>
                void run(async () => {
                  await api.del(`/plan-requests/${request.id}`)
                  setData({ ...plan, myRequest: { ...request, status: 'withdrawn' } })
                })
              }
            >
              {t('plan.withdraw')}
            </Button>
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

      {user && <PlanRequestSheet plan={plan} meId={user.id} open={requesting} onClose={() => setRequesting(false)} onSent={setData} />}

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

/** Add to calendar and share to WhatsApp (PLAN.md §4.3). The calendar file has the exact meeting point only for participants. */
function PlanShareRow({ plan }: { plan: Plan }) {
  const { t, i18n } = useTranslation()
  const [copied, setCopied] = useState(false)
  const url = `${window.location.origin}/plans/${plan.id}`
  const text = planShareText(plan.title, formatPlanWhen(plan.localDate, plan.localTime, i18n.language), url)

  const copy = () => {
    void navigator.clipboard?.writeText(url)
    setCopied(true)
  }

  return (
    <div className="screen__row plan__share">
      <a className="btn btn--secondary btn--sm" href={`/api/plans/${plan.id}/calendar.ics`} download>
        <Icon name="calendar" size={18} />
        <span>{t('plan.addToCalendar')}</span>
      </a>
      <a className="btn btn--secondary btn--sm" href={whatsAppLink(text)} target="_blank" rel="noopener noreferrer">
        <Icon name="share" size={18} />
        <span>{t('share.whatsapp')}</span>
      </a>
      <Button size="sm" variant="ghost" onClick={copy}>
        {copied ? t('share.copied') : t('share.copy')}
      </Button>
    </div>
  )
}
