import { useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { useMe } from '../auth/useAuth'
import { BottomSheet, Button, Chip, FormError, Icon, IconButton, Segmented } from '../components'
import { CardForm, type CardFields } from '../features/CardForm'
import { CreateSheet } from '../features/CreateSheet'
import { JoinRequest } from '../features/JoinRequest'
import { PersonItem } from '../features/PersonItem'
import { ShareSheet } from '../features/ShareSheet'
import { api, errorCode } from '../lib/api'
import { countryName } from '../lib/countries'
import { formatDateRange, tintFor } from '../lib/dates'
import type { Card, CardMember, CardRequest, CardRole } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './TripScreen.css'

type Tab = 'plans' | 'members' | 'chat'

/** The cover band shared by the trip page and its public preview. */
export function TripCover({ card, backTo, action }: { card: Pick<Card, 'id' | 'coverUrl'>; backTo: string; action?: ReactNode }) {
  const { t } = useTranslation()
  return (
    <div
      className="trip__cover"
      style={{ background: card.coverUrl ? `center / cover no-repeat url("${card.coverUrl}")` : tintFor(card.id) }}
    >
      {!card.coverUrl && (
        <svg viewBox="0 0 390 200" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
          <path d="M0 140 L80 96 L150 128 L230 80 L310 124 L390 100 L390 200 L0 200Z" fill="#9CC3C9" />
          <path d="M0 170 L110 130 L190 160 L280 120 L390 160 L390 200 L0 200Z" fill="#6E9EA5" />
        </svg>
      )}
      <Link to={backTo} className="icon-btn icon-btn--raised trip__back" aria-label={t('common.back')}>
        <Icon name="back" />
      </Link>
      {action}
    </div>
  )
}

/** 4 · Vacation Card, inside view for members: plans, members, chat, share/QR (PLAN.md §4.2). */
export function TripScreen() {
  const { t, i18n } = useTranslation()
  const { tripId } = useParams()
  const me = useMe()
  const navigate = useNavigate()
  const { data: card, error, setData } = useApi<Card>(`/cards/${tripId}`)
  const [tab, setTab] = useState<Tab>('plans')
  const [sharing, setSharing] = useState(false)
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [confirmLeave, setConfirmLeave] = useState(false)
  const [managing, setManaging] = useState<CardMember | null>(null)
  const [confirmHandOver, setConfirmHandOver] = useState(false)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const coverRef = useRef<HTMLInputElement>(null)
  const canDecide = card?.access === 'owner' || card?.access === 'coAdmin'
  const requests = useApi<CardRequest[]>(canDecide ? `/cards/${tripId}/requests` : null)

  if (error === 'NotFound') return <NotFoundScreen />
  if (!card) {
    return (
      <p className="screen__loading" role="status">
        {error ? <FormError code={error} /> : t('common.loading')}
      </p>
    )
  }

  // A preview-level viewer (public card, not a member) gets the public page instead.
  if (card.access === 'preview') return <TripPreview card={card} footer={<JoinRequest card={card} onChange={setData} />} />

  const isOwner = card.access === 'owner'
  const members = card.members ?? []

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

  const save = (fields: CardFields) =>
    run(async () => {
      setData(await api.patch<Card>(`/cards/${card.id}`, fields))
      setEditing(false)
    })

  const decide = (request: CardRequest, verdict: 'approve' | 'reject') =>
    run(async () => {
      setData(await api.post<Card>(`/card-requests/${request.id}/${verdict}`))
      requests.reload()
    })

  const closeManage = () => {
    setManaging(null)
    setConfirmHandOver(false)
  }

  const setRole = (member: CardMember, role: CardRole) =>
    run(async () => {
      setData(await api.put<Card>(`/cards/${card.id}/members/${member.person.id}/role`, { role }))
      closeManage()
    })

  const remove = (member: CardMember) =>
    run(async () => {
      setData(await api.del<Card>(`/cards/${card.id}/members/${member.person.id}`))
      closeManage()
    })

  const uploadCover = (file: File) =>
    run(async () => {
      const form = new FormData()
      form.append('file', file)
      setData(await api.post<Card>(`/cards/${card.id}/cover`, form))
    })

  return (
    <div className="screen">
      <TripCover
        card={card}
        backTo="/"
        action={<IconButton icon="share" label={t('trip.share')} variant="raised" className="trip__share" onClick={() => setSharing(true)} />}
      />

      <section className="screen__section">
        <div className="screen__row">
          <Chip tone={card.visibility === 'public' ? 'success' : 'neutral'}>{card.visibility === 'public' ? t('trip.public') : t('trip.inviteOnly')}</Chip>
          <Chip>{countryName(card.countryCode, i18n.language)}</Chip>
        </div>
        <h1 className="trip__name">{card.name}</h1>
        <p className="screen__meta">
          {formatDateRange(card.startsOn, card.endsOn, i18n.language)} · {card.regions.join(', ')}
        </p>
        {card.description && <p className="screen__body">{card.description}</p>}
        {isOwner && (
          <div className="screen__row">
            <Button size="sm" variant="secondary" onClick={() => setEditing(true)}>
              {t('trip.edit')}
            </Button>
            <input
              ref={coverRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              aria-label={t('trip.coverInput')}
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) void uploadCover(file)
                e.target.value = ''
              }}
            />
            <Button size="sm" variant="secondary" loading={busy && !editing} onClick={() => coverRef.current?.click()}>
              {card.coverUrl ? t('trip.changeCover') : t('trip.addCover')}
            </Button>
          </div>
        )}
        {!editing && <FormError code={actionError} />}
      </section>

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
          <p className="screen__empty">{t('trip.noPlans')}</p>
          <Button variant="secondary" icon="plus" onClick={() => setCreating(true)}>
            {t('create.planTitle')}
          </Button>
        </section>
      )}

      {tab === 'members' && (
        <section className="screen__section" aria-label={t('trip.tabMembers', { count: members.length })}>
          {canDecide && !!requests.data?.length && (
            <>
              <h2 className="screen__section-title">{t('trip.requests', { count: requests.data.length })}</h2>
              <ul className="list-reset screen__stack">
                {requests.data.map((r) => (
                  <li key={r.id} className="screen__stack">
                    <PersonItem person={r.person} />
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
              <h2 className="screen__section-title">{t('trip.members')}</h2>
            </>
          )}
          <ul className="list-reset screen__stack">
            {members.map((m) => (
              <li key={m.person.id}>
                <PersonItem
                  person={m.person}
                  you={m.person.id === me.id}
                  trailing={
                    <span className="screen__row">
                      <Chip>{t(`trip.role_${m.role}`)}</Chip>
                      {isOwner && m.person.id !== me.id && (
                        <IconButton icon="more" label={t('trip.manageMember', { name: m.person.displayName })} onClick={() => setManaging(m)} />
                      )}
                    </span>
                  }
                />
              </li>
            ))}
          </ul>
          {!isOwner && (
            <Button variant="ghost" onClick={() => setConfirmLeave(true)}>
              {t('trip.leave')}
            </Button>
          )}
        </section>
      )}

      {tab === 'chat' && (
        <section className="screen__section" aria-label={t('trip.tabChat')}>
          <p className="screen__meta">{t('trip.chatHint')}</p>
          <Link to={`/inbox/card-${card.id}`} className="btn btn--secondary">
            {t('trip.openChat')}
          </Link>
        </section>
      )}

      {isOwner && (
        <section className="screen__section">
          <Button variant="ghost" onClick={() => setConfirmDelete(true)}>
            {t('trip.delete')}
          </Button>
        </section>
      )}
      <div className="screen__section" />

      {card.shareSlug && (
        <ShareSheet
          card={{ ...card, shareSlug: card.shareSlug }}
          open={sharing}
          onClose={() => setSharing(false)}
          onNewLink={isOwner ? () => void run(async () => setData(await api.post<Card>(`/cards/${card.id}/share-link`))) : undefined}
        />
      )}
      <CreateSheet open={creating} onClose={() => setCreating(false)} initialMode="plan" />

      <BottomSheet
        open={editing}
        onClose={() => setEditing(false)}
        title={t('trip.edit')}
        footer={
          <Button size="lg" block type="submit" form="edit-card" loading={busy}>
            {t('profile.save')}
          </Button>
        }
      >
        <CardForm id="edit-card" initial={card} error={actionError} onSubmit={(f) => void save(f)} />
      </BottomSheet>

      <BottomSheet
        open={confirmDelete}
        onClose={() => setConfirmDelete(false)}
        title={t('trip.deleteTitle')}
        footer={
          <div className="screen__stack">
            <Button block size="lg" variant="brand" onClick={() => setConfirmDelete(false)}>
              {t('trip.keep')}
            </Button>
            <Button
              block
              variant="ghost"
              loading={busy}
              onClick={() =>
                void run(async () => {
                  await api.del(`/cards/${card.id}`)
                  void navigate('/', { replace: true })
                })
              }
            >
              {t('trip.deleteConfirm')}
            </Button>
          </div>
        }
      >
        <p className="screen__body">{t('trip.deleteBody')}</p>
      </BottomSheet>

      <BottomSheet
        open={confirmLeave}
        onClose={() => setConfirmLeave(false)}
        title={t('trip.leaveTitle')}
        footer={
          <div className="screen__stack">
            <Button block size="lg" variant="brand" onClick={() => setConfirmLeave(false)}>
              {t('trip.stay')}
            </Button>
            <Button
              block
              variant="ghost"
              loading={busy}
              onClick={() =>
                void run(async () => {
                  await api.post(`/cards/${card.id}/leave`)
                  void navigate('/', { replace: true })
                })
              }
            >
              {t('trip.leave')}
            </Button>
          </div>
        }
      >
        <p className="screen__body">{t('trip.leaveBody')}</p>
        <FormError code={actionError} />
      </BottomSheet>

      <BottomSheet open={managing !== null} onClose={closeManage} title={managing?.person.displayName ?? ''}>
        {managing && (
          <div className="screen__stack">
            {confirmHandOver ? (
              <>
                <p className="screen__body">{t('trip.handOverBody', { name: managing.person.displayName })}</p>
                <Button block loading={busy} onClick={() => void setRole(managing, 'owner')}>
                  {t('trip.handOverConfirm')}
                </Button>
              </>
            ) : (
              <>
                {managing.role === 'member' ? (
                  <Button block variant="secondary" loading={busy} onClick={() => void setRole(managing, 'coAdmin')}>
                    {t('trip.makeCoAdmin')}
                  </Button>
                ) : (
                  <Button block variant="secondary" loading={busy} onClick={() => void setRole(managing, 'member')}>
                    {t('trip.makeMember')}
                  </Button>
                )}
                <Button block variant="secondary" disabled={busy} onClick={() => setConfirmHandOver(true)}>
                  {t('trip.handOver')}
                </Button>
                <Button block variant="ghost" disabled={busy} onClick={() => void remove(managing)}>
                  {t('trip.remove')}
                </Button>
              </>
            )}
            <FormError code={actionError} />
          </div>
        )}
      </BottomSheet>
    </div>
  )
}

/** The public face of a card: what a visitor or non-member sees (name, where, when, who's going). */
export function TripPreview({ card, footer }: { card: Card; footer?: ReactNode }) {
  const { t, i18n } = useTranslation()
  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <TripCover card={card} backTo="/welcome" />
      <section className="screen__section">
        <div className="screen__row">
          <Chip>{countryName(card.countryCode, i18n.language)}</Chip>
        </div>
        <h1 className="trip__name">{card.name}</h1>
        <p className="screen__meta">
          {formatDateRange(card.startsOn, card.endsOn, i18n.language)} · {card.regions.join(', ')}
        </p>
        {card.description && <p className="screen__body">{card.description}</p>}
        <p className="screen__meta">{t('trip.travelers', { count: card.memberCount })}</p>
      </section>
      <section className="screen__section">
        <p className="screen__note">{t('trip.previewNote')}</p>
      </section>
      {footer && <footer className="screen__footer">{footer}</footer>}
    </div>
  )
}
