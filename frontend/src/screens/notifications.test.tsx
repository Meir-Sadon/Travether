import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import type { AppNotification, NotificationSettings } from '../lib/types'
import { mockApi, reply } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

const request: AppNotification = {
  id: 'n-1',
  type: 'plan_request',
  payload: { url: '/inbox', actor: 'Lena', subject: 'Sunrise hike' },
  readAt: null,
  createdAt: '2026-10-06T10:00:00Z',
}
const reminder: AppNotification = {
  id: 'n-2',
  type: 'plan_reminder',
  payload: { url: '/plans/p-1', subject: 'Cooking class', count: 2 },
  readAt: '2026-10-06T09:00:00Z',
  createdAt: '2026-10-06T08:00:00Z',
}
const settings: NotificationSettings = {
  requests: true,
  messages: true,
  matches: true,
  reminders: true,
  reviews: true,
  email: true,
  quietFrom: '22:00:00',
  quietTo: '08:00:00',
  timeZone: 'UTC',
}

describe('notifications', () => {
  it('lists notifications and marks one read when opened', async () => {
    const api = mockApi({
      'GET /notifications': { items: [request, reminder], unread: 1, hasMore: false },
      'POST /notifications/read': reply(204),
      'GET /inbox/requests': { incoming: [], outgoing: [] },
      'GET /chats': [],
    })
    renderAt('/notifications')

    expect(await screen.findByText('Lena asked to join Sunrise hike')).toBeInTheDocument()
    expect(screen.getByText('Cooking class starts in about 2 hours')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('link', { name: /Lena asked to join/ }))

    expect(api.calls.find((c) => c.path === '/notifications/read')?.body).toEqual({ ids: ['n-1'] })
  })

  it('shows the unread count on the Home bell', async () => {
    mockApi({ 'GET /cards': [], 'GET /notifications/unread': { count: 3 }, 'GET /chats': [], 'GET /inbox/requests': { incoming: [], outgoing: [] } })
    renderAt('/')

    expect(await screen.findByRole('link', { name: 'Notifications, 3 new' })).toHaveAttribute('href', '/notifications')
  })

  it('saves a category as soon as it is switched, with the device time zone', async () => {
    const api = mockApi({
      'GET /notifications/settings': settings,
      'PUT /notifications/settings': (body: unknown) => body,
    })
    renderAt('/settings')

    await userEvent.click(await screen.findByRole('switch', { name: /Chat messages/ }))

    const put = api.calls.find((c) => c.method === 'PUT')
    expect(put?.body).toMatchObject({ messages: false, requests: true, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone })
    expect(screen.getByRole('switch', { name: /Chat messages/ })).not.toBeChecked()
    // jsdom has no Push API.
    expect(screen.getByText(/can't show push notifications/)).toBeInTheDocument()
  })

  it('turns quiet hours off', async () => {
    const api = mockApi({ 'GET /notifications/settings': settings, 'PUT /notifications/settings': (body: unknown) => body })
    renderAt('/settings')

    await userEvent.click(await screen.findByRole('switch', { name: /Quiet hours/ }))

    expect(api.calls.find((c) => c.method === 'PUT')?.body).toMatchObject({ quietFrom: null, quietTo: null })
  })
})
