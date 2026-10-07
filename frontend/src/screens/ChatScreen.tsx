import { useEffect, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { useMe } from '../auth/useAuth'
import { Avatar, BottomSheet, Button, FormError, IconButton } from '../components'
import { ReportSheet, type ReportTarget } from '../features/ReportSheet'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { useChatEvents } from '../lib/chatHub'
import { mapLink } from '../lib/plans'
import type { Chat, ChatMessage } from '../lib/types'
import { useApi } from '../lib/useApi'
import { NotFoundScreen } from './NotFoundScreen'
import './ChatScreen.css'

/** "card-<id>" or "plan-<id>" → API path segment. */
function chatPath(key: string | undefined): string | null {
  const match = /^(card|plan)-([0-9a-f-]{36})$/.exec(key ?? '')
  return match ? `/chats/${match[1]}/${match[2]}` : null
}

function addMessage(list: ChatMessage[], m: ChatMessage): ChatMessage[] {
  return list.some((x) => x.id === m.id) ? list : [...list, m]
}

/** Card or plan chat, live over SignalR. Contact details are shared only by choice (PLAN.md decision 3). */
export function ChatScreen() {
  const { t, i18n } = useTranslation()
  const { chatId } = useParams()
  const me = useMe()
  const path = chatPath(chatId)
  const { data: chat, error, setData } = useApi<Chat>(path)
  const [draft, setDraft] = useState('')
  const [sending, setSending] = useState(false)
  const [sendError, setSendError] = useState<string | null>(null)
  const [sharing, setSharing] = useState(false)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const [reporting, setReporting] = useState<ReportTarget | null>(null)
  const lastId = chat?.messages.at(-1)?.id

  useChatEvents((e) => {
    if (chat && e.chat === chat.key) setData({ ...chat, messages: addMessage(chat.messages, e.message) })
  }, !!path)

  // Everything on screen counts as read.
  useEffect(() => {
    if (path && lastId) void api.post(`${path}/read`).catch(() => {})
  }, [path, lastId])

  if (!path || error === 'NotFound') return <NotFoundScreen />
  if (!chat) {
    return (
      <p className="screen__loading" role="status">
        {error ? <FormError code={error} /> : t('common.loading')}
      </p>
    )
  }

  const time = new Intl.DateTimeFormat(i18n.language, { hour: '2-digit', minute: '2-digit' })
  const target = chat.type === 'card' ? `/trips/${chat.refId}` : `/plans/${chat.refId}`

  const post = async (url: string, body: unknown) => {
    setSending(true)
    setSendError(null)
    try {
      const message = await api.post<ChatMessage>(url, body)
      setData({ ...chat, messages: addMessage(chat.messages, message) })
      return true
    } catch (err) {
      setSendError(errorCode(err))
      return false
    } finally {
      setSending(false)
    }
  }

  const send = async (e: FormEvent) => {
    e.preventDefault()
    const body = draft.trim()
    if (!body) return
    if (await post(`${path}/messages`, { body })) setDraft('')
  }

  const share = async (kind: 'phone' | 'whatsapp') => {
    if (await post(`${path}/contact`, { kind })) setSharing(false)
  }

  const loadOlder = async () => {
    setLoadingOlder(true)
    try {
      const older = await api.get<Chat>(`${path}?before=${encodeURIComponent(chat.messages[0].createdAt)}`)
      setData({ ...chat, messages: [...older.messages, ...chat.messages], hasMore: older.hasMore })
    } finally {
      setLoadingOlder(false)
    }
  }

  return (
    <div className="screen chat">
      <ScreenHeader back backTo="/inbox" title={chat.title} subtitle={t('chat.people', { count: chat.memberCount })} />
      <p className="chat__pinned">
        {chat.meetingPoint ? (
          <>
            <strong>{t('chat.meetingPoint')}</strong> {chat.meetingPoint.name} ·{' '}
            <a href={mapLink(chat.meetingPoint.lat, chat.meetingPoint.lng)} target="_blank" rel="noreferrer">
              {t('plan.openMap')}
            </a>
          </>
        ) : null}{' '}
        <Link to={target}>{chat.type === 'card' ? t('chat.openTrip') : t('chat.openPlan')}</Link>
      </p>
      <ol className="list-reset chat__messages" aria-live="polite">
        {chat.hasMore && (
          <li className="chat__system">
            <Button size="sm" variant="ghost" loading={loadingOlder} onClick={() => void loadOlder()}>
              {t('chat.earlier')}
            </Button>
          </li>
        )}
        <li className="chat__system">{t('chat.safety')}</li>
        {chat.messages.length === 0 && <li className="chat__system">{t('chat.empty')}</li>}
        {chat.messages.map((m, i) => {
          const mine = m.sender.id === me.id
          const firstOfRun = chat.messages[i - 1]?.sender.id !== m.sender.id
          const name = m.sender.displayName || t('chat.deletedUser')
          return (
            <li key={m.id} className={`chat__msg${mine ? ' chat__msg--mine' : ''}`}>
              {!mine &&
                (firstOfRun ? (
                  <Avatar person={{ name, photoUrl: m.sender.photoUrl ?? undefined, tint: 'var(--color-accent-soft)' }} size="sm" />
                ) : (
                  <span className="chat__avatar-gap" />
                ))}
              <div className="chat__bubble">
                {!mine && firstOfRun && <span className="chat__sender">{name}</span>}
                <MessageBody message={m} />
                <span className="chat__time">
                  {time.format(new Date(m.createdAt))}
                  {!mine && (
                    <button
                      type="button"
                      className="chat__report"
                      aria-label={t('chat.reportMessage', { name })}
                      onClick={() => setReporting({ type: 'message', id: m.id })}
                    >
                      {t('chat.report')}
                    </button>
                  )}
                </span>
              </div>
            </li>
          )
        })}
      </ol>
      <div className="chat__tools">
        <Button size="sm" variant="secondary" onClick={() => setSharing(true)}>
          {t('chat.shareNumber')}
        </Button>
      </div>
      <FormError code={sharing ? null : sendError} />
      <form className="chat__composer" onSubmit={(e) => void send(e)}>
        <label htmlFor="chat-input" className="visually-hidden">
          {t('chat.message')}
        </label>
        <input
          id="chat-input"
          className="chat__input"
          value={draft}
          maxLength={2000}
          onChange={(e) => setDraft(e.target.value)}
          placeholder={t('chat.message')}
          autoComplete="off"
        />
        <IconButton icon="send" label={t('chat.send')} type="submit" className="chat__send" disabled={sending} />
      </form>

      <BottomSheet open={sharing} onClose={() => setSharing(false)} title={t('chat.shareNumber')}>
        <div className="screen__stack">
          {me.phone ? (
            <>
              <p className="screen__meta">{t('chat.shareHint', { number: me.phone })}</p>
              <Button block loading={sending} onClick={() => void share('whatsapp')}>
                {t('chat.shareWhatsapp')}
              </Button>
              <Button block variant="secondary" disabled={sending} onClick={() => void share('phone')}>
                {t('chat.sharePhone')}
              </Button>
            </>
          ) : (
            <>
              <p className="screen__meta">{t('chat.noPhone')}</p>
              <Link to="/profile/edit" className="btn btn--secondary btn--block">
                {t('chat.addPhone')}
              </Link>
            </>
          )}
          <FormError code={sendError} />
        </div>
      </BottomSheet>
      <ReportSheet target={reporting} onClose={() => setReporting(null)} />
    </div>
  )
}

/** Text, or a shared number as a call or WhatsApp link. */
function MessageBody({ message }: { message: ChatMessage }) {
  const { t } = useTranslation()
  if (message.kind === 'text') return <span>{message.body}</span>
  const digits = message.body.replace(/[^0-9]/g, '')
  const whatsapp = message.kind === 'contactWhatsapp'
  return (
    <span>
      {whatsapp ? t('chat.sharedWhatsapp') : t('chat.sharedPhone')}{' '}
      <a href={whatsapp ? `https://wa.me/${digits}` : `tel:${message.body}`} target={whatsapp ? '_blank' : undefined} rel="noreferrer">
        {message.body}
      </a>
    </span>
  )
}
