import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { AvatarStack, Button, Card, CardBody, Chip, Icon, IconButton, Segmented } from '../components'
import { CreateSheet } from '../features/CreateSheet'
import { PersonRow } from '../features/PersonRow'
import { ShareSheet } from '../features/ShareSheet'
import { categoryTint, me, messages, person, plans, seatsLeft, trip as findTrip, trips } from '../mock/data'
import { NotFoundScreen } from './NotFoundScreen'
import './TripScreen.css'

type Tab = 'plans' | 'members' | 'chat'

/**
 * 4 · Vacation Card: cover, plans, members, chat, share/QR (PLAN.md §4.2).
 * With `preview`, the public page a visitor sees from a share link or QR code.
 */
export function TripScreen({ preview = false }: { preview?: boolean }) {
  const { t } = useTranslation()
  const { tripId, slug } = useParams()
  const tr = preview ? trips.find((x) => x.shareSlug === slug) : findTrip(tripId)
  const [tab, setTab] = useState<Tab>('plans')
  const [request, setRequest] = useState<'pending' | 'approved' | 'declined'>('pending')
  const [sharing, setSharing] = useState(false)
  const [creating, setCreating] = useState(false)

  if (!tr) return <NotFoundScreen />

  const members = tr.members.map((m) => ({ ...m, person: person(m.id) }))
  if (request === 'approved') members.push({ id: 'lena', role: 'member', person: person('lena') })
  const tripPlans = plans.filter((p) => p.tripId === tr.id)

  return (
    <div className="screen" style={{ minBlockSize: preview ? '100dvh' : undefined }}>
      <div className="trip__cover" style={{ background: tr.tint }}>
        <svg viewBox="0 0 390 200" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
          <path d="M0 140 L80 96 L150 128 L230 80 L310 124 L390 100 L390 200 L0 200Z" fill="#9CC3C9" />
          <path d="M0 170 L110 130 L190 160 L280 120 L390 160 L390 200 L0 200Z" fill="#6E9EA5" />
        </svg>
        <Link to={preview ? '/welcome' : '/'} className="icon-btn icon-btn--raised trip__back" aria-label={t('common.back')}>
          <Icon name="back" />
        </Link>
        {!preview && <IconButton icon="share" label={t('trip.share')} variant="raised" className="trip__share" onClick={() => setSharing(true)} />}
      </div>

      <section className="screen__section">
        <div className="screen__row">
          <Chip tone={tr.visibility === 'public' ? 'success' : 'neutral'}>{tr.visibility === 'public' ? t('trip.public') : t('trip.inviteOnly')}</Chip>
          <Chip>{tr.country}</Chip>
        </div>
        <h1 className="trip__name">{tr.name}</h1>
        <p className="screen__meta">
          {tr.dates} · {tr.regions.join(', ')}
        </p>
        <p className="screen__body">{tr.description}</p>
        {preview && (
          <div className="trip__preview-meta">
            <AvatarStack people={members.map((m) => m.person)} size="md" />
            <span className="screen__meta">{t('trip.previewMeta', { members: members.length, plans: tripPlans.length })}</span>
          </div>
        )}
      </section>

      {preview ? (
        <>
          <section className="screen__section">
            <p className="screen__note">{t('trip.previewNote')}</p>
          </section>
          <footer className="screen__footer">
            <Link to="/signup" className="btn btn--primary btn--lg btn--block">
              {t('trip.requestToJoin')}
            </Link>
          </footer>
        </>
      ) : (
        <>
          <div className="screen__section">
            <Segmented
              label={t('trip.sections')}
              value={tab}
              onChange={setTab}
              options={[
                { value: 'plans', label: t('trip.tabPlans') },
                { value: 'members', label: t('trip.tabMembers', { count: members.length }) },
                { value: 'chat', label: t('trip.tabChat') },
              ]}
            />
          </div>

          {tab === 'plans' && (
            <section className="screen__section" aria-label={t('trip.tabPlans')}>
              <ul className="list-reset screen__stack">
                {tripPlans.map((p) => (
                  <Card as="li" key={p.id} interactive>
                    <Link to={`/plans/${p.id}`} className="trip__plan">
                      <span className="trip__plan-tile" style={{ background: categoryTint[p.category] }}>
                        {t(`category.${p.category}`)}
                      </span>
                      <span className="trip__plan-text">
                        <strong>{p.title}</strong>
                        <span className="screen__meta">
                          {p.when} · {t(`plan.audience_${p.audience}`)}
                        </span>
                        <span className="screen__meta">{t('trip.seats', { going: p.goingIds.length, total: p.seatLimit, left: seatsLeft(p) })}</span>
                      </span>
                    </Link>
                  </Card>
                ))}
              </ul>
              <Button variant="secondary" icon="plus" onClick={() => setCreating(true)}>
                {t('create.planTitle')}
              </Button>
            </section>
          )}

          {tab === 'members' && (
            <section className="screen__section" aria-label={t('trip.tabMembers', { count: members.length })}>
              {request === 'pending' && (
                <Card variant="filled">
                  <CardBody>
                    <strong>{t('trip.joinRequest')}</strong>
                    <PersonRow person={person('lena')} />
                    <p className="screen__body">“{t('trip.lenaMessage')}”</p>
                    <div className="screen__row">
                      <Button size="sm" variant="secondary" onClick={() => setRequest('declined')}>
                        {t('common.decline')}
                      </Button>
                      <Button size="sm" variant="brand" onClick={() => setRequest('approved')}>
                        {t('common.approve')}
                      </Button>
                    </div>
                  </CardBody>
                </Card>
              )}
              <ul className="list-reset screen__stack">
                {members.map((m) => (
                  <li key={m.id}>
                    <PersonRow person={m.person} you={m.id === me.id} trailing={<Chip>{t(`trip.role_${m.role}`)}</Chip>} />
                  </li>
                ))}
              </ul>
            </section>
          )}

          {tab === 'chat' && (
            <section className="screen__section" aria-label={t('trip.tabChat')}>
              <ul className="list-reset screen__stack">
                {(messages[tr.id] ?? []).map((m, i) => (
                  <li key={i} className="screen__meta">
                    <strong>{person(m.from).name}:</strong> {m.text}
                  </li>
                ))}
              </ul>
              <Link to={`/inbox/${tr.id}`} className="btn btn--secondary">
                {t('trip.openChat')}
              </Link>
            </section>
          )}
          <ShareSheet trip={tr} open={sharing} onClose={() => setSharing(false)} />
          <CreateSheet open={creating} onClose={() => setCreating(false)} initialMode="plan" />
        </>
      )}
    </div>
  )
}
