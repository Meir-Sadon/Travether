import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button, Card, CardBody, Chip, Segmented } from '../components'
import { PersonRow } from '../features/PersonRow'
import { ScreenHeader } from '../layout/ScreenHeader'
import { categoryTint, chats, incomingRequests, person } from '../mock/data'
import './InboxScreen.css'

type Tab = 'requests' | 'chats'

/** 7 · Inbox: join requests to decide, my own requests, and chats. */
export function InboxScreen() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('requests')
  const [decided, setDecided] = useState<Record<string, 'approved' | 'declined'>>({})

  return (
    <div className="screen">
      <ScreenHeader large title={t('inbox.title')} />
      <div className="screen__section">
        <Segmented
          label={t('inbox.title')}
          value={tab}
          onChange={setTab}
          options={[
            { value: 'requests', label: t('inbox.requests', { count: incomingRequests.length }) },
            { value: 'chats', label: t('inbox.chats') },
          ]}
        />
      </div>

      {tab === 'requests' ? (
        <>
          <section className="screen__section">
            <Link to="/plans/sanctuary/review" className="inbox__prompt">
              <strong>{t('inbox.didYouMeet', { group: 'Berlin Backpackers' })}</strong>
              <span className="screen__meta">{t('inbox.didYouMeetMeta', { plan: 'Elephant sanctuary' })}</span>
            </Link>
          </section>

          <section className="screen__section" aria-labelledby="inbox-waiting">
            <h2 id="inbox-waiting" className="screen__section-title">
              {t('inbox.waiting')}
            </h2>
            <ul className="list-reset screen__stack">
              {incomingRequests.map((r) => {
                const p = person(r.personId)
                const d = decided[r.id]
                return (
                  <Card as="li" key={r.id}>
                    <CardBody>
                      <PersonRow person={p} meta={`${r.party > 1 ? t('inbox.plus', { count: r.party - 1 }) : ''}${p.flag} ${p.country}`} />
                      <span className="screen__meta">{t('inbox.wantsToJoin', { target: r.target })}</span>
                      <p className="screen__body">“{r.message}”</p>
                      {d ? (
                        <span className={`inbox__decided inbox__decided--${d}`} role="status">
                          {d === 'approved' ? t('inbox.approved') : t('inbox.declined')}
                        </span>
                      ) : (
                        <div className="screen__row">
                          <Button size="sm" variant="secondary" onClick={() => setDecided({ ...decided, [r.id]: 'declined' })}>
                            {t('common.decline')}
                          </Button>
                          <Button size="sm" variant="brand" onClick={() => setDecided({ ...decided, [r.id]: 'approved' })}>
                            {t('common.approve')}
                          </Button>
                        </div>
                      )}
                    </CardBody>
                  </Card>
                )
              })}
            </ul>
          </section>

          <section className="screen__section" aria-labelledby="inbox-sent">
            <h2 id="inbox-sent" className="screen__section-title">
              {t('inbox.youAsked')}
            </h2>
            <Card>
              <Link to="/plans/khao-soi" className="inbox__sent">
                <Chip tint={categoryTint.food}>{t('category.food')}</Chip>
                <span className="inbox__sent-text">
                  <strong>Khao soi tasting tour</strong>
                  <span className="screen__meta">{t('inbox.sentMeta', { host: 'Sofía', ago: '2 h' })}</span>
                </span>
                <Chip tone="accent">{t('inbox.pending')}</Chip>
              </Link>
            </Card>
          </section>
        </>
      ) : (
        <ul className="list-reset screen__section">
          {chats.map((c) => (
            <li key={c.id}>
              <Link to={`/inbox/${c.id}`} className="inbox__chat">
                <span className="inbox__chat-tile" style={{ background: c.category ? categoryTint[c.category] : 'var(--tint-tour)' }}>
                  {c.kind === 'trip' ? t('inbox.trip') : t(`category.${c.category ?? 'other'}`)}
                </span>
                <span className="inbox__chat-text">
                  <span className="inbox__chat-top">
                    <strong>{c.title}</strong>
                    <span className="screen__meta">{c.time}</span>
                  </span>
                  <span className="screen__meta inbox__chat-last">{c.last}</span>
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
