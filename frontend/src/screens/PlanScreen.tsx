import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { Button, Card, CardBody, Chip, Icon, IconButton, Stepper } from '../components'
import { PersonRow } from '../features/PersonRow'
import { categoryTint, person, plan as findPlan, seatsLeft } from '../mock/data'
import { NotFoundScreen } from './NotFoundScreen'
import './PlanScreen.css'

type Status = 'idle' | 'requested' | 'approved'

/** 6 · Activity Plan: details, hosts, join request with status steps (PLAN.md §4.3, §4.5). */
export function PlanScreen() {
  const { t } = useTranslation()
  const { planId } = useParams()
  const p = findPlan(planId)
  const [status, setStatus] = useState<Status>('idle')
  const [party, setParty] = useState<'me' | 'group'>('me')

  if (!p) return <NotFoundScreen />

  const approved = status === 'approved'
  const left = seatsLeft(p)
  const stepIndex = status === 'approved' ? 2 : status === 'requested' ? 1 : 0

  return (
    <div className="screen plan" style={{ minBlockSize: '100dvh' }}>
      <div className="plan__cover" style={{ background: categoryTint[p.category] }}>
        <Link to="/discover" className="icon-btn icon-btn--raised plan__back" aria-label={t('common.back')}>
          <Icon name="back" />
        </Link>
        <IconButton icon="more" label={t('plan.reportOrBlock')} variant="raised" className="plan__more" />
      </div>

      <section className="screen__section">
        <span>
          <Chip tint={categoryTint[p.category]}>{t(`category.${p.category}`)}</Chip>
        </span>
        <h1 className="plan__title">{p.title}</h1>
        <ul className="list-reset plan__facts">
          <li>
            <Icon name="calendar" size={18} />
            {p.when}
          </li>
          <li>
            <Icon name="pin" size={18} />
            {approved ? (
              <span>
                <strong>{t('plan.meet', { place: p.exactMeetingPoint })}</strong>
              </span>
            ) : (
              <span>
                {t('plan.area', { area: p.area, distance: p.distance })}
                <span className="screen__meta plan__private">{t('plan.exactAfterApproval')}</span>
              </span>
            )}
          </li>
          <li>
            <Icon name="users" size={18} />
            {t('plan.going', { going: p.goingIds.length, left })} · {t(`plan.audience_${p.audience}`)}
          </li>
        </ul>
        <p className="screen__body plan__purpose">{p.purpose}</p>
      </section>

      <section className="screen__section" aria-labelledby="plan-hosts">
        <h2 id="plan-hosts" className="screen__section-title">
          {t('plan.hostedBy', { group: p.hostGroup })}
        </h2>
        <ul className="list-reset screen__stack">
          {p.hostIds.map((id) => {
            const h = person(id)
            return (
              <li key={id}>
                <Link to="/profile" className="plan__person-link">
                  <PersonRow
                    person={h}
                    meta={`${h.flag} ${h.country} · ${h.rating !== null ? t('plan.rating', { rating: h.rating, count: h.reviewCount }) : t('plan.newOnTravether')}`}
                    trailing={
                      h.badges.includes('id') ? (
                        <Chip tone="success" icon="shieldCheck">
                          {t('badge.id')}
                        </Chip>
                      ) : h.badges.includes('photo') ? (
                        <Chip tone="success" icon="shieldCheck">
                          {t('badge.photo')}
                        </Chip>
                      ) : undefined
                    }
                  />
                </Link>
              </li>
            )
          })}
        </ul>
      </section>

      {status !== 'idle' && (
        <section className="screen__section">
          <Card>
            <CardBody>
              <strong>{t('plan.yourRequest')}</strong>
              <div className="plan__stepper">
                <Stepper label={t('plan.requestStatus')} steps={[t('plan.stepRequested'), t('plan.stepApproved'), t('plan.stepChat')]} current={stepIndex} />
              </div>
              {status === 'requested' && (
                <>
                  <p className="screen__meta plan__center">{t('plan.waiting', { hosts: p.hostIds.map((id) => person(id).name).join(' / ') })}</p>
                  <button type="button" className="plan__simulate" onClick={() => setStatus('approved')}>
                    {t('plan.simulate')}
                  </button>
                </>
              )}
            </CardBody>
          </Card>
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
        {p.bookable && (
          <Card>
            <CardBody className="plan__book">
              <span>
                <strong>{t('plan.bookTogether')}</strong>
                <span className="screen__meta">{t('plan.bookVia', { item: p.bookable })}</span>
              </span>
              <Icon name="arrowOut" />
            </CardBody>
          </Card>
        )}
      </section>

      <footer className="screen__footer">
        {status === 'idle' && (
          <>
            <div className="plan__party" role="radiogroup" aria-label={t('plan.whoIsComing')}>
              {(['me', 'group'] as const).map((v) => (
                <button key={v} type="button" role="radio" aria-checked={party === v} className="plan__party-option" onClick={() => setParty(v)}>
                  {t(`plan.party_${v}`)}
                </button>
              ))}
            </div>
            <Button size="lg" block onClick={() => setStatus('requested')}>
              {party === 'me' ? t('plan.request') : t('plan.requestSeats', { count: 3 })}
            </Button>
          </>
        )}
        {status === 'requested' && (
          <Button variant="secondary" size="lg" block onClick={() => setStatus('idle')}>
            {t('plan.withdraw')}
          </Button>
        )}
        {approved && (
          <Link to={`/inbox/${p.id}`} className="btn btn--primary btn--lg btn--block">
            <Icon name="chat" />
            {t('plan.openChat')}
          </Link>
        )}
      </footer>
    </div>
  )
}
