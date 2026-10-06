import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import { crewCard, crewTile, lena, noa, previewOf } from '../test/fixtures'
import { mockApi, reply } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('vacation cards', () => {
  it('lists my trips on Home', async () => {
    mockApi({ 'GET /cards': [crewTile] })
    renderAt('/')
    expect(await screen.findByText('Chiang Mai Crew')).toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1, name: 'Hi Noa' })).toBeInTheDocument()
    expect(screen.getByText('0 plans')).toBeInTheDocument()
  })

  it('creates a trip from the + sheet and opens it', async () => {
    const api = mockApi({ 'GET /cards': [], 'POST /cards': crewCard, [`GET /cards/${crewCard.id}`]: crewCard })
    renderAt('/')
    await userEvent.click(await screen.findByRole('button', { name: 'New Vacation Card' }))
    const sheet = screen.getByRole('dialog', { name: 'New Vacation Card' })
    await userEvent.type(within(sheet).getByLabelText('Trip name'), 'Chiang Mai Crew')
    await userEvent.selectOptions(within(sheet).getByLabelText('Country'), 'TH')
    await userEvent.type(within(sheet).getByLabelText('Cities or regions'), 'Chiang Mai, Pai')
    await userEvent.type(within(sheet).getByLabelText('From'), '2026-10-12')
    await userEvent.type(within(sheet).getByLabelText('To'), '2026-10-26')
    await userEvent.click(within(sheet).getByRole('radio', { name: 'Invite only' }))
    await userEvent.click(screen.getByRole('button', { name: 'Create trip' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Chiang Mai Crew' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'POST' && c.path === '/cards')?.body).toEqual({
      name: 'Chiang Mai Crew',
      countryCode: 'TH',
      regions: ['Chiang Mai', 'Pai'],
      startsOn: '2026-10-12',
      endsOn: '2026-10-26',
      description: '',
      visibility: 'inviteOnly',
    })
  })

  it('shows members and lets the owner edit the trip', async () => {
    const api = mockApi({
      [`GET /cards/${crewCard.id}`]: crewCard,
      [`PATCH /cards/${crewCard.id}`]: { ...crewCard, name: 'CM Crew' },
    })
    renderAt(`/trips/${crewCard.id}`)
    await userEvent.click(await screen.findByRole('radio', { name: 'Members · 2' }))
    expect(screen.getByRole('link', { name: 'Lena' })).toHaveAttribute('href', '/people/u-lena')

    await userEvent.click(screen.getByRole('button', { name: 'Edit trip' }))
    const name = within(screen.getByRole('dialog', { name: 'Edit trip' })).getByLabelText('Trip name')
    await userEvent.clear(name)
    await userEvent.type(name, 'CM Crew')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'CM Crew' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'PATCH')?.body).toMatchObject({ name: 'CM Crew' })
  })

  it('opens a share link as a public preview for visitors', async () => {
    mockApi({ 'GET /auth/me': { user: null }, 'GET /cards/share/abcDEF2345': previewOf(crewCard) })
    renderAt('/c/abcDEF2345')
    expect(await screen.findByRole('heading', { level: 1, name: 'Chiang Mai Crew' })).toBeInTheDocument()
    expect(screen.getByText('2 travelers')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Sign up to request to join' })).toHaveAttribute('href', '/signup?next=%2Fc%2FabcDEF2345')
    expect(screen.queryByRole('radio', { name: /Members/ })).not.toBeInTheDocument()
  })

  it('treats an unknown or retired share link as not found', async () => {
    mockApi({ 'GET /cards/share/old': reply(404, { code: 'NotFound' }) })
    renderAt('/c/old')
    expect(await screen.findByRole('heading', { level: 1, name: 'Nothing here' })).toBeInTheDocument()
  })
})

describe('joining a trip', () => {
  const preview = { ...previewOf(crewCard), visibility: 'inviteOnly' as const }

  it('sends a join request from the share link and can withdraw it', async () => {
    const sent = { ...preview, myRequest: { id: 'req-1', status: 'requested' as const, createdAt: '2026-10-06T10:00:00Z' } }
    const api = mockApi({
      'GET /cards/share/abcDEF2345': preview,
      [`POST /cards/${crewCard.id}/requests`]: sent,
      'DELETE /card-requests/req-1': reply(204),
    })
    renderAt('/c/abcDEF2345')
    await userEvent.click(await screen.findByRole('button', { name: 'Request to join this trip' }))
    const sheet = screen.getByRole('dialog', { name: 'Request to join this trip' })
    await userEvent.type(within(sheet).getByLabelText('A note to the group (optional)'), 'Hi!')
    await userEvent.click(screen.getByRole('button', { name: 'Send request' }))

    expect(await screen.findByText('Request sent. The group will see it and decide.')).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'POST')?.body).toEqual({ message: 'Hi!', shareSlug: 'abcDEF2345' })

    await userEvent.click(screen.getByRole('button', { name: 'Withdraw request' }))
    expect(await screen.findByRole('button', { name: 'Request to join this trip' })).toBeInTheDocument()
  })

  it('lets a co-admin approve a request', async () => {
    const asCoAdmin = { ...crewCard, access: 'coAdmin' as const, pendingRequestCount: 1 }
    const api = mockApi({
      [`GET /cards/${crewCard.id}`]: asCoAdmin,
      [`GET /cards/${crewCard.id}/requests`]: [
        { id: 'req-1', person: { ...lena, id: 'u-max', displayName: 'Max' }, message: 'Solo hiker here', status: 'requested', createdAt: '2026-10-06T10:00:00Z' },
      ],
      'POST /card-requests/req-1/approve': { ...asCoAdmin, memberCount: 3, pendingRequestCount: 0 },
    })
    renderAt(`/trips/${crewCard.id}`)
    await userEvent.click(await screen.findByRole('radio', { name: 'Members · 2' }))
    expect(await screen.findByText('Solo hiker here')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Manage/ })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    expect(api.calls.some((c) => c.method === 'POST' && c.path === '/card-requests/req-1/approve')).toBe(true)
  })

  it('lets the owner hand over ownership', async () => {
    const handedOver = {
      ...crewCard,
      access: 'coAdmin' as const,
      members: [
        { person: lena, role: 'owner' as const, joinedAt: '2026-10-02T00:00:00Z' },
        { person: noa, role: 'coAdmin' as const, joinedAt: '2026-10-01T00:00:00Z' },
      ],
    }
    const api = mockApi({
      [`GET /cards/${crewCard.id}`]: crewCard,
      [`PUT /cards/${crewCard.id}/members/${lena.id}/role`]: handedOver,
    })
    renderAt(`/trips/${crewCard.id}`)
    await userEvent.click(await screen.findByRole('radio', { name: 'Members · 2' }))
    await userEvent.click(screen.getByRole('button', { name: 'Manage Lena' }))
    await userEvent.click(screen.getByRole('button', { name: 'Hand over ownership' }))
    await userEvent.click(screen.getByRole('button', { name: 'Hand over' }))

    expect(api.calls.find((c) => c.method === 'PUT')?.body).toEqual({ role: 'owner' })
    expect(await screen.findByText('Co-admin')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit trip' })).not.toBeInTheDocument()
  })

  it('lets a member leave', async () => {
    const asMember = { ...crewCard, access: 'member' as const }
    const api = mockApi({ [`GET /cards/${crewCard.id}`]: asMember, [`POST /cards/${crewCard.id}/leave`]: reply(204), 'GET /cards': [] })
    renderAt(`/trips/${crewCard.id}`)
    await userEvent.click(await screen.findByRole('radio', { name: 'Members · 2' }))
    await userEvent.click(screen.getByRole('button', { name: 'Leave trip' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Leave this trip?' })).getByRole('button', { name: 'Leave trip' }))

    expect(await screen.findByText(/No trips yet/)).toBeInTheDocument()
    expect(api.calls.some((c) => c.path === `/cards/${crewCard.id}/leave`)).toBe(true)
  })

  it('shows open requests on the Home tile', async () => {
    mockApi({ 'GET /cards': [{ ...crewTile, pendingRequests: 2 }] })
    renderAt('/')
    expect(await screen.findByText('2 requests')).toBeInTheDocument()
  })
})
