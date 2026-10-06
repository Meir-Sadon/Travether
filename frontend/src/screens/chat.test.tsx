import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import type { Chat, ChatMessage, ChatSummary, InboxRequest } from '../lib/types'
import { lena, noa } from '../test/fixtures'
import { mockApi, reply, testUser } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

const planId = '3f2a9c1e-0000-4000-8000-000000000001'
const fromLena: ChatMessage = { id: 'm-1', sender: { id: lena.id, displayName: 'Lena', photoUrl: null }, body: 'See you at the gate!', kind: 'text', createdAt: '2026-10-06T10:00:00Z' }
const chat: Chat = {
  key: `plan-${planId}`,
  type: 'plan',
  refId: planId,
  title: 'Sunrise hike to Doi Suthep',
  memberCount: 2,
  meetingPoint: { name: 'Tha Phae Gate', lat: 18.7877, lng: 98.9933 },
  messages: [fromLena],
  hasMore: false,
}
const path = `/chats/plan/${planId}`

describe('chat', () => {
  it('shows the conversation and sends a message', async () => {
    const mine: ChatMessage = { ...fromLena, id: 'm-2', sender: { id: noa.id, displayName: 'Noa', photoUrl: null }, body: 'On my way' }
    const api = mockApi({ [`GET ${path}`]: chat, [`POST ${path}/messages`]: mine, [`POST ${path}/read`]: reply(204) })
    renderAt(`/inbox/plan-${planId}`)

    expect(await screen.findByText('See you at the gate!')).toBeInTheDocument()
    expect(screen.getByText(/Tha Phae Gate/)).toBeInTheDocument()
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'On my way{Enter}')

    expect(await screen.findByText('On my way')).toBeInTheDocument()
    expect(screen.getByRole('textbox', { name: 'Message' })).toHaveValue('')
    expect(api.calls.find((c) => c.method === 'POST' && c.path.endsWith('/messages'))?.body).toEqual({ body: 'On my way' })
    expect(api.calls.some((c) => c.path === `${path}/read`)).toBe(true)
  })

  it('shares my WhatsApp only when I choose to', async () => {
    const shared: ChatMessage = { ...fromLena, id: 'm-3', sender: { id: noa.id, displayName: 'Noa', photoUrl: null }, body: '+972501234567', kind: 'contactWhatsapp' }
    const api = mockApi({
      'GET /auth/me': { user: { ...testUser, phone: '+972501234567' } },
      [`GET ${path}`]: chat,
      [`POST ${path}/contact`]: shared,
      [`POST ${path}/read`]: reply(204),
    })
    renderAt(`/inbox/plan-${planId}`)
    await userEvent.click(await screen.findByRole('button', { name: 'Share my number' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Share my number' })).getByRole('button', { name: 'Share my WhatsApp' }))

    expect(await screen.findByRole('link', { name: '+972501234567' })).toHaveAttribute('href', 'https://wa.me/972501234567')
    expect(api.calls.find((c) => c.path === `${path}/contact`)?.body).toEqual({ kind: 'whatsapp' })
  })

  it('asks for a phone number before sharing one', async () => {
    mockApi({ [`GET ${path}`]: chat, [`POST ${path}/read`]: reply(204) })
    renderAt(`/inbox/plan-${planId}`)
    await userEvent.click(await screen.findByRole('button', { name: 'Share my number' }))
    expect(screen.getByRole('link', { name: 'Add phone number' })).toHaveAttribute('href', '/profile/edit')
  })
})

describe('inbox', () => {
  const request: InboxRequest = {
    id: 'cr-1',
    target: 'card',
    targetId: 'card-1',
    targetTitle: 'Chiang Mai Crew',
    category: null,
    person: lena,
    partySize: 1,
    message: 'Solo in Chiang Mai the same dates!',
    status: 'requested',
    createdAt: '2026-10-06T10:00:00Z',
  }
  const summary: ChatSummary = {
    key: `plan-${planId}`,
    type: 'plan',
    refId: planId,
    title: 'Sunrise hike to Doi Suthep',
    category: 'hike',
    last: { senderName: 'Lena', mine: false, body: 'See you at the gate!', kind: 'text', createdAt: '2026-10-06T10:00:00Z' },
    unread: 2,
    updatedAt: '2026-10-06T10:00:00Z',
  }

  it('approves a join request from the inbox', async () => {
    const api = mockApi({
      'GET /inbox/requests': { incoming: [request], outgoing: [] },
      'GET /chats': [summary],
      'POST /card-requests/cr-1/approve': {},
    })
    renderAt('/inbox')
    expect(await screen.findByText('“Solo in Chiang Mai the same dates!”')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    expect(api.calls.some((c) => c.method === 'POST' && c.path === '/card-requests/cr-1/approve')).toBe(true)
  })

  it('lists chats with unread counts', async () => {
    mockApi({ 'GET /inbox/requests': { incoming: [], outgoing: [] }, 'GET /chats': [summary] })
    renderAt('/inbox')
    const link = await screen.findByRole('link', { name: /Sunrise hike to Doi Suthep/ })
    expect(link).toHaveAttribute('href', `/inbox/plan-${planId}`)
    expect(within(link).getByText('Lena: See you at the gate!')).toBeInTheDocument()
    expect(within(link).getByLabelText('2 unread')).toBeInTheDocument()
  })
})
