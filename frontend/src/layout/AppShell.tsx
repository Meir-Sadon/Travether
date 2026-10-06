import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router'
import { useAuth } from '../auth/useAuth'
import { Icon, type IconName } from '../components'
import { CreateSheet } from '../features/CreateSheet'
import { useChatEvents } from '../lib/chatHub'
import type { ChatSummary, InboxRequest } from '../lib/types'
import { useApi } from '../lib/useApi'
import './AppShell.css'

type Tab = { to: string; icon: IconName; label: 'nav.home' | 'nav.discover' | 'nav.inbox' | 'nav.profile'; badge?: number }

/** Signed-in layout: content plus the thumb-reachable bottom navigation (PLAN.md §5). */
export function AppShell() {
  const { t } = useTranslation()
  const [creating, setCreating] = useState(false)
  const { user } = useAuth()
  const { pathname } = useLocation()
  const chats = useApi<ChatSummary[]>(user ? '/chats' : null)
  const requests = useApi<{ incoming: InboxRequest[] }>(user ? '/inbox/requests' : null)
  const inboxCount = (requests.data?.incoming.length ?? 0) + (chats.data ?? []).reduce((n, c) => n + c.unread, 0)
  const { reload: reloadChats } = chats
  const { reload: reloadRequests } = requests

  // The badge catches up when you move between screens and when a message arrives.
  const firstVisit = useRef(true)
  useEffect(() => {
    if (!user) return
    if (firstVisit.current) {
      firstVisit.current = false
      return
    }
    reloadChats()
    reloadRequests()
  }, [pathname, user, reloadChats, reloadRequests])
  useChatEvents(() => reloadChats(), !!user)

  const tabs: Tab[] = [
    { to: '/', icon: 'home', label: 'nav.home' },
    { to: '/discover', icon: 'compass', label: 'nav.discover' },
    { to: '/inbox', icon: 'chat', label: 'nav.inbox', badge: inboxCount },
    { to: '/profile', icon: 'user', label: 'nav.profile' },
  ]

  const link = (tab: Tab) => (
    <NavLink key={tab.to} to={tab.to} end={tab.to === '/'} className="nav__item">
      <span className="nav__icon">
        <Icon name={tab.icon} size={24} />
        {tab.badge ? (
          <span className="nav__badge" aria-label={t('nav.unread', { count: tab.badge })}>
            {tab.badge}
          </span>
        ) : null}
      </span>
      {t(tab.label)}
    </NavLink>
  )

  return (
    <div className="shell">
      <div className="shell__content">
        <Outlet />
      </div>
      <nav className="nav" aria-label={t('nav.label')}>
        {tabs.slice(0, 2).map(link)}
        <button type="button" className="nav__create" aria-label={t('nav.create')} onClick={() => setCreating(true)}>
          <Icon name="plus" size={24} />
        </button>
        {tabs.slice(2).map(link)}
      </nav>
      <CreateSheet open={creating} onClose={() => setCreating(false)} />
    </div>
  )
}

/** Full-screen pages without the bottom bar (landing, sign-up, plan, chat). */
export function BareShell() {
  return (
    <div className="shell shell--bare">
      <Outlet />
    </div>
  )
}
