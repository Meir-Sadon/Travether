import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button, Icon, type IconName } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api } from '../lib/api'
import { useNotificationEvents } from '../lib/chatHub'
import type { AppNotification, NotificationPage } from '../lib/types'
import { useApi } from '../lib/useApi'
import './NotificationsScreen.css'

const icons: Record<string, IconName> = {
  card_request: 'users',
  plan_request: 'users',
  plan_joined: 'users',
  card_request_approved: 'check',
  plan_request_approved: 'check',
  card_request_rejected: 'close',
  plan_request_rejected: 'close',
  plan_cancelled: 'close',
  plan_reminder: 'clock',
  matches_digest: 'compass',
  meet_prompt: 'star',
  review_received: 'star',
}

const knownTypes = new Set(Object.keys(icons))

/** In-app notifications (PLAN.md §4.7): newest first; opening one marks it read and goes where it points. */
export function NotificationsScreen() {
  const { t, i18n } = useTranslation()
  const page = useApi<NotificationPage>('/notifications')
  const [older, setOlder] = useState<AppNotification[]>([])
  const [hasMoreOlder, setHasMoreOlder] = useState<boolean | null>(null)
  const items = [...(page.data?.items ?? []), ...older]
  const hasMore = hasMoreOlder ?? page.data?.hasMore ?? false
  const when = new Intl.DateTimeFormat(i18n.language, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })

  useNotificationEvents(() => page.reload())

  const text = (n: AppNotification) => {
    const p = { actor: n.payload.actor, subject: n.payload.subject, count: n.payload.count ?? 0 }
    if (!knownTypes.has(n.type)) return t('notifications.type_other')
    // Types are server-defined strings; the keys above are listed in en.json.
    return t(`notifications.type_${n.type}` as 'notifications.type_other', p)
  }

  const markRead = (ids: string[] | null) => {
    void api.post('/notifications/read', { ids }).then(() => page.reload())
  }

  const loadMore = async () => {
    const last = items.at(-1)
    if (!last) return
    const next = await api.get<NotificationPage>(`/notifications?before=${encodeURIComponent(last.createdAt)}`)
    setOlder((o) => [...o, ...next.items])
    setHasMoreOlder(next.hasMore)
  }

  return (
    <div className="screen">
      <ScreenHeader
        back
        title={t('notifications.title')}
        action={
          page.data && page.data.unread > 0 ? (
            <Button size="sm" variant="ghost" onClick={() => markRead(null)}>
              {t('notifications.markAllRead')}
            </Button>
          ) : undefined
        }
      />

      {page.data && items.length === 0 && <p className="screen__empty">{t('notifications.empty')}</p>}
      <ul className="list-reset notifications">
        {items.map((n) => (
          <li key={n.id}>
            <Link
              to={n.payload.url.startsWith('/') ? n.payload.url : '/'}
              className={`notifications__item${n.readAt ? '' : ' notifications__item--unread'}`}
              onClick={() => !n.readAt && markRead([n.id])}
            >
              <span className="notifications__icon" aria-hidden="true">
                <Icon name={icons[n.type] ?? 'bell'} size={20} />
              </span>
              <span className="notifications__text">
                <span>{text(n)}</span>
                <span className="screen__meta">{when.format(new Date(n.createdAt))}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      {hasMore && (
        <Button block variant="ghost" onClick={() => void loadMore()}>
          {t('notifications.loadMore')}
        </Button>
      )}
    </div>
  )
}
