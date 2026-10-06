import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { Avatar, Button, IconButton } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { chats, me, messages, person, type MockMessage } from '../mock/data'
import { NotFoundScreen } from './NotFoundScreen'
import './ChatScreen.css'

/** Chat for a card or plan. Contact details are shared only by choice (PLAN.md decision 3). */
export function ChatScreen() {
  const { t } = useTranslation()
  const { chatId } = useParams()
  const chat = chats.find((c) => c.id === chatId)
  const [list, setList] = useState<MockMessage[]>(() => messages[chatId ?? ''] ?? [])
  const [draft, setDraft] = useState('')

  if (!chat) return <NotFoundScreen />

  const send = (e: FormEvent) => {
    e.preventDefault()
    const text = draft.trim()
    if (!text) return
    setList([...list, { from: me.id, text, time: t('chat.now') }])
    setDraft('')
  }

  const shareNumber = () => setList([...list, { from: me.id, text: t('chat.sharedNumber', { number: '+972 ••• ••• 381' }), time: t('chat.now') }])

  return (
    <div className="screen chat">
      <ScreenHeader back backTo="/inbox" title={chat.title} subtitle={t('chat.people', { count: chat.memberIds.length })} />
      {chat.meetingPoint && (
        <p className="chat__pinned">
          <strong>{t('chat.meetingPoint')}</strong> {chat.meetingPoint}
        </p>
      )}
      <ol className="list-reset chat__messages" aria-live="polite">
        <li className="chat__system">{t('chat.safety')}</li>
        {list.map((m, i) => {
          const mine = m.from === me.id
          const sender = person(m.from)
          const firstOfRun = list[i - 1]?.from !== m.from
          return (
            <li key={i} className={`chat__msg${mine ? ' chat__msg--mine' : ''}`}>
              {!mine && (firstOfRun ? <Avatar person={{ name: sender.name, tint: sender.tint }} size="sm" /> : <span className="chat__avatar-gap" />)}
              <div className="chat__bubble">
                {!mine && firstOfRun && <span className="chat__sender">{sender.name}</span>}
                <span>{m.text}</span>
                <span className="chat__time">{m.time}</span>
              </div>
            </li>
          )
        })}
      </ol>
      <div className="chat__tools">
        <Button size="sm" variant="secondary" onClick={shareNumber}>
          {t('chat.shareNumber')}
        </Button>
        <Button size="sm" variant="secondary">
          {t('chat.sharePlan')}
        </Button>
      </div>
      <form className="chat__composer" onSubmit={send}>
        <label htmlFor="chat-input" className="visually-hidden">
          {t('chat.message')}
        </label>
        <input id="chat-input" className="chat__input" value={draft} onChange={(e) => setDraft(e.target.value)} placeholder={t('chat.message')} autoComplete="off" />
        <IconButton icon="send" label={t('chat.send')} type="submit" className="chat__send" />
      </form>
    </div>
  )
}
