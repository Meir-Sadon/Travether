import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import type { Chat, ChatMessage } from '../lib/types'
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
const fromLena: ChatMessage = { id: 'm-1', sender: { id: lena.id, displayName: 'Lena', photoUrl: null }, body: 'Send me your passport', kind: 'text', createdAt: '2026-10-06T10:00:00Z' }
const chat: Chat = { key: `plan-${planId}`, type: 'plan', refId: planId, title: 'Hike', memberCount: 2, meetingPoint: null, messages: [fromLena], hasMore: false }

describe('safety', () => {
  it('reports a chat message with a reason', async () => {
    const api = mockApi({ [`GET /chats/plan/${planId}`]: chat, [`POST /chats/plan/${planId}/read`]: reply(204), 'POST /reports': reply(204) })
    renderAt(`/inbox/plan-${planId}`)

    await userEvent.click(await screen.findByRole('button', { name: 'Report message from Lena' }))
    const sheet = screen.getByRole('dialog', { name: 'Report message' })
    await userEvent.click(within(sheet).getByRole('radio', { name: 'Scam, money or ID requests' }))
    await userEvent.click(screen.getByRole('button', { name: 'Send report' }))

    expect(api.calls.find((c) => c.path === '/reports')?.body).toEqual({ targetType: 'message', targetId: 'm-1', reason: 'scam', details: '' })
    expect(await screen.findByText(/A moderator will look at it/)).toBeInTheDocument()
  })

  it('blocks someone from their profile after confirming', async () => {
    const api = mockApi({
      [`GET /users/${lena.id}`]: { ...lena, bio: null, languages: [], interests: [], rating: { average: null, count: 0 }, fullName: null, createdAt: '2026-01-01T00:00:00Z' },
      [`GET /users/${lena.id}/reviews`]: { items: [], hasMore: false },
      [`POST /users/${lena.id}/block`]: reply(204),
    })
    renderAt(`/people/${lena.id}`)

    await userEvent.click(await screen.findByRole('button', { name: 'Block Lena' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Block Lena?' })).getByRole('button', { name: 'Block' }))

    expect(api.calls.some((c) => c.method === 'POST' && c.path === `/users/${lena.id}/block`)).toBe(true)
  })

  it('lets a moderator remove reported content with a reason', async () => {
    const report = {
      id: 'r-1',
      targetType: 'message',
      targetId: 'm-1',
      reason: 'scam',
      details: 'Asked for my passport',
      status: 'open',
      createdAt: '2026-10-06T10:00:00Z',
      reporter: noa,
      target: { text: 'Send me your passport', author: lena, authorBanned: false, removed: false },
      openReportsOnTarget: 2,
      resolutionNote: null,
    }
    const api = mockApi({
      'GET /auth/me': { user: { ...testUser, role: 'moderator' } },
      'GET /admin/reports': [report],
      'POST /admin/reports/r-1/resolve': reply(204),
    })
    renderAt('/admin')

    const item = await screen.findByRole('listitem', { name: 'Message report: Scam, money or ID requests' })
    expect(within(item).getByText('Send me your passport')).toBeInTheDocument()
    expect(within(item).getByText('2 reports')).toBeInTheDocument()
    expect(within(item).getByRole('button', { name: 'Remove' })).toBeDisabled()
    await userEvent.type(within(item).getByRole('textbox', { name: /Reason for the decision/ }), 'No ID requests.')
    await userEvent.click(within(item).getByRole('button', { name: 'Remove' }))

    expect(api.calls.find((c) => c.path === '/admin/reports/r-1/resolve')?.body).toEqual({ action: 'remove', note: 'No ID requests.' })
  })

  it('hides moderation from everyone else', async () => {
    mockApi()
    renderAt('/admin')
    expect(await screen.findByRole('heading', { name: 'Nothing here' })).toBeInTheDocument()
  })

  it('unblocks from the blocked list', async () => {
    const api = mockApi({ 'GET /me/blocks': [{ id: lena.id, displayName: 'Lena', photoUrl: null, blockedAt: '2026-10-01T00:00:00Z' }], [`DELETE /users/${lena.id}/block`]: reply(204) })
    renderAt('/settings/blocked')

    await userEvent.click(await screen.findByRole('button', { name: 'Unblock Lena' }))

    expect(api.calls.some((c) => c.method === 'DELETE' && c.path === `/users/${lena.id}/block`)).toBe(true)
    expect(await screen.findByText("You haven't blocked anyone.")).toBeInTheDocument()
  })
})
