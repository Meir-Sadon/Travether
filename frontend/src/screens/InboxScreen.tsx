import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button, Card, CardBody, Chip, FormError, Segmented } from '../components'
import { PersonItem } from '../features/PersonItem'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { useChatEvents } from '../lib/chatHub'
import { categoryTint } from '../lib/plans'
import type { ChatSummary, InboxRequest } from '../lib/types'
import { useApi } from '../lib/useApi'
import './InboxScreen.css'

type Tab = 'requests' | 'chats'

const targetLink = (r: InboxRequest) => (r.target === 'card' ? `/trips/${r.targetId}` : `/plans/${r.targetId}`)

/** 7 · Inbox: join requests to decide, my own requests, and chats. */
export function InboxScreen() {
  const { t, i18n } = useTranslation()
  const requests = useApi<{ incoming: InboxRequest[]; outgoing: InboxRequest[] }>('/inbox/requests')
  const chats = useApi<ChatSummary[]>('/chats')
  const incoming = requests.data?.incoming ?? []
  const outgoing = requests.data?.outgoing ?? []
  const [tab, setTab] = useState<Tab | null>(null)
  const current: Tab = tab ?? (requests.data && incoming.length === 0 ? 'chats' : 'requests')
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  useChatEvents(() => chats.reload())

  const decide = async (r: InboxRequest, verdict: 'approve' | 'reject') => {
    setBusy(r.id)
    setError(null)
    try {
      await api.post(`/${r.target}-requests/${r.id}/${verdict}`)
      requests.reload()
    } catch (err) {
      setError(errorCode(err))
      requests.reload()
    } finally {
      setBusy(null)
    }
  }

  const when = new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  return (
    <div className="screen">
      <ScreenHeader large title={t('inbox.title')} />
      <div className="screen__section">
        <Segmented
          label={t('inbox.title')}
          value={current}
          onChange={setTab}
          options={[
            { value: 'requests', label: t('inbox.requests', { count: incoming.length }) },
            { value: 'chats', label: t('inbox.chats') },
          ]}
        />
      </div>

      {current === 'requests' ? (
        <>
          <section className="screen__section" aria-labelledby="inbox-waiting">
            <h2 id="inbox-waiting" className="screen__section-title">
              {t('inbox.waiting')}
            </h2>
            <FormError code={error} />
            {requests.data && incoming.length === 0 && <p className="screen__empty">{t('inbox.noneWaiting')}</p>}
            <ul className="list-reset screen__stack">
              {incoming.map((r) => (
                <Card as="li" key={r.id}>
                  <CardBody>
                    <PersonItem
                      person={r.person}
                      trailing={r.partySize > 1 ? <Chip>{t('plan.partySize', { count: r.partySize })}</Chip> : undefined}
                    />
                    <Link to={targetLink(r)} className="screen__meta">
                      {t('inbox.wantsToJoin', { target: r.targetTitle })}
                    </Link>
                    {r.message && <p className="screen__body">“{r.message}”</p>}
                    <div className="screen__row">
                      <Button size="sm" variant="secondary" disabled={busy === r.id} onClick={() => void decide(r, 'reject')}>
                        {t('common.decline')}
                      </Button>
                      <Button size="sm" variant="brand" loading={busy === r.id} onClick={() => void decide(r, 'approve')}>
                        {t('common.approve')}
                      </Button>
                    </div>
                  </CardBody>
                </Card>
              ))}
            </ul>
          </section>

          {outgoing.length > 0 && (
            <section className="screen__section" aria-labelledby="inbox-sent">
              <h2 id="inbox-sent" className="screen__section-title">
                {t('inbox.youAsked')}
              </h2>
              <ul className="list-reset screen__stack">
                {outgoing.map((r) => (
                  <Card as="li" key={r.id}>
                    <Link to={targetLink(r)} className="inbox__sent">
                      {r.category ? <Chip tint={categoryTint[r.category]}>{t(`category.${r.category}`)}</Chip> : <Chip>{t('inbox.trip')}</Chip>}
                      <span className="inbox__sent-text">
                        <strong>{r.targetTitle}</strong>
                        <span className="screen__meta">{t('inbox.sentAt', { when: when.format(new Date(r.createdAt)) })}</span>
                      </span>
                      <Chip tone={r.status === 'approved' ? 'success' : r.status === 'requested' ? 'accent' : 'neutral'}>{t(`inbox.status_${r.status}`)}</Chip>
                    </Link>
                  </Card>
                ))}
              </ul>
            </section>
          )}
        </>
      ) : (
        <ul className="list-reset screen__section">
          {chats.data?.length === 0 && <li className="screen__empty">{t('inbox.noChats')}</li>}
          {chats.data?.map((c) => (
            <li key={c.key}>
              <Link to={`/inbox/${c.key}`} className="inbox__chat">
                <span className="inbox__chat-tile" style={{ background: c.category ? categoryTint[c.category] : 'var(--tint-tour)' }}>
                  {c.category ? t(`category.${c.category}`) : t('inbox.trip')}
                </span>
                <span className="inbox__chat-text">
                  <span className="inbox__chat-top">
                    <strong>{c.title}</strong>
                    <span className="screen__meta">{when.format(new Date(c.updatedAt))}</span>
                  </span>
                  <span className="screen__meta inbox__chat-last">
                    {c.last
                      ? `${c.last.mine ? t('inbox.you') : c.last.senderName}: ${c.last.kind === 'text' ? c.last.body : t('inbox.sharedContact')}`
                      : t('inbox.noMessages')}
                  </span>
                </span>
                {c.unread > 0 && (
                  <span className="inbox__unread" aria-label={t('nav.unread', { count: c.unread })}>
                    {c.unread}
                  </span>
                )}
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
