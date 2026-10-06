import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import { crewCard, crewTile, hikePlan, hikeSummary, lena, noa, publicPlan } from '../test/fixtures'
import { mockApi, reply } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('activity plans', () => {
  it('shows outsiders the area and distance, never the meeting point', async () => {
    mockApi({ 'GET /plans/plan-1': publicPlan })
    renderAt('/plans/plan-1')
    expect(await screen.findByRole('heading', { level: 1, name: 'Sunrise hike to Doi Suthep' })).toBeInTheDocument()
    expect(screen.getByText(/Old City, Chiang Mai · ~2 km from you/)).toBeInTheDocument()
    expect(screen.getByText("Exact meeting point shown once you're approved")).toBeInTheDocument()
    expect(screen.getByText('Destination shared with participants')).toBeInTheDocument()
    expect(screen.queryByText(/Tha Phae Gate/)).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit plan' })).not.toBeInTheDocument()
  })

  it('asks visitors to sign up', async () => {
    mockApi({ 'GET /auth/me': { user: null }, 'GET /plans/plan-1': publicPlan })
    renderAt('/plans/plan-1')
    expect(await screen.findByRole('link', { name: 'Sign up to join' })).toHaveAttribute('href', '/signup?next=%2Fplans%2Fplan-1')
  })

  it('lets a trip member take a seat and then shows the meeting point', async () => {
    const api = mockApi({
      'GET /plans/plan-1': { ...publicPlan, canSelfJoin: true, participants: [lena] },
      'POST /plans/plan-1/join': { ...hikePlan, access: 'participant', canManage: false, host: lena, participants: [lena, noa] },
    })
    renderAt('/plans/plan-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Join this plan' }))

    expect(await screen.findByText('Meet: Tha Phae Gate')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open plan chat' })).toHaveAttribute('href', '/inbox/plan-plan-1')
    expect(screen.getByRole('button', { name: 'Leave plan' })).toBeInTheDocument()
    expect(api.calls.some((c) => c.method === 'POST' && c.path === '/plans/plan-1/join')).toBe(true)
  })

  it('creates a plan from the trip with a searched meeting point', async () => {
    const api = mockApi({
      [`GET /cards/${crewCard.id}`]: crewCard,
      [`GET /cards/${crewCard.id}/plans`]: [],
      'GET /places/search': [{ name: 'Tha Phae Gate', area: 'Old City, Chiang Mai', lat: 18.7877, lng: 98.9933 }],
      [`POST /cards/${crewCard.id}/plans`]: hikePlan,
      'GET /plans/plan-1': hikePlan,
    })
    renderAt(`/trips/${crewCard.id}`)
    await userEvent.click(await screen.findByRole('button', { name: 'New Activity Plan' }))
    const sheet = screen.getByRole('dialog', { name: 'New Activity Plan' })
    await userEvent.type(within(sheet).getByLabelText('Title'), 'Sunrise hike to Doi Suthep')
    await userEvent.type(within(sheet).getByLabelText('Date'), '2026-10-14')
    await userEvent.type(within(sheet).getByLabelText('Time'), '05:30')
    await userEvent.type(within(sheet).getByLabelText('Meeting point'), 'Tha')
    await userEvent.click(await within(sheet).findByRole('button', { name: /Tha Phae Gate/ }))
    expect(within(sheet).getByText('Others will see: Old City, Chiang Mai')).toBeInTheDocument()
    await userEvent.type(within(sheet).getByLabelText('Destination', { selector: 'input' }), 'Wat Phra That Doi Suthep')
    await userEvent.click(within(sheet).getByRole('checkbox', { name: 'Lena' }))
    await userEvent.click(screen.getByRole('button', { name: 'Publish plan' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sunrise hike to Doi Suthep' })).toBeInTheDocument()
    expect(screen.getByText('Meet: Tha Phae Gate')).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'POST')?.body).toEqual({
      title: 'Sunrise hike to Doi Suthep',
      category: 'hike',
      origin: { lat: 18.7877, lng: 98.9933 },
      originName: 'Tha Phae Gate',
      originAreaLabel: 'Old City, Chiang Mai',
      destination: 'Wat Phra That Doi Suthep',
      destinationPrecision: 'exact',
      date: '2026-10-14',
      time: '05:30',
      purpose: '',
      seatLimit: 6,
      audience: 'open',
      participantIds: [lena.id],
    })
  })

  it("lists the trip's plans", async () => {
    mockApi({ [`GET /cards/${crewCard.id}`]: crewCard, [`GET /cards/${crewCard.id}/plans`]: [hikeSummary] })
    renderAt(`/trips/${crewCard.id}`)
    expect(await screen.findByText('Sunrise hike to Doi Suthep')).toBeInTheDocument()
    expect(screen.getByText("You're going")).toBeInTheDocument()
  })

  it('lets the host cancel the plan', async () => {
    mockApi({ 'GET /plans/plan-1': hikePlan, 'POST /plans/plan-1/cancel': { ...hikePlan, status: 'cancelled' } })
    renderAt('/plans/plan-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Cancel plan' }))
    const dialog = screen.getByRole('dialog', { name: 'Cancel this plan?' })
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancel plan' }))

    expect(await screen.findByText('Cancelled')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit plan' })).not.toBeInTheDocument()
  })

  it('asks which trip a plan from the + button belongs to', async () => {
    mockApi({ 'GET /cards': [{ ...crewTile, endsOn: '2999-01-01' }], [`GET /cards/${crewCard.id}`]: crewCard })
    renderAt('/')
    await userEvent.click(await screen.findByRole('button', { name: 'Create a plan or trip' }))
    await userEvent.click(screen.getByRole('button', { name: /New Activity Plan/ }))
    const sheet = screen.getByRole('dialog', { name: 'New Activity Plan' })
    expect(screen.getByRole('button', { name: 'Publish plan' })).toBeDisabled()

    await userEvent.selectOptions(within(sheet).getByLabelText('Which trip is this plan for?'), crewCard.id)
    expect(await within(sheet).findByText('in Chiang Mai Crew')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish plan' })).toBeEnabled()
  })
})

describe('plan requests', () => {
  const myTrip = { ...crewCard, id: 'card-mine', name: 'Pai Pals', access: 'owner' as const }
  const myTripTile = { ...crewTile, id: 'card-mine', name: 'Pai Pals', endsOn: '2999-01-01' }

  it('sends a solo request, shows its status and withdraws it', async () => {
    const api = mockApi({
      'GET /plans/plan-1': publicPlan,
      'POST /plans/plan-1/requests': { ...publicPlan, myRequest: { id: 'pr-1', status: 'requested', partySize: 1, createdAt: '2026-10-06T10:00:00Z' } },
      'DELETE /plan-requests/pr-1': reply(204),
    })
    renderAt('/plans/plan-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Request to join' }))
    const sheet = screen.getByRole('dialog', { name: 'Request to join' })
    await userEvent.type(within(sheet).getByLabelText('A note to the host (optional)'), 'Hi!')
    await userEvent.click(within(sheet).getByRole('button', { name: 'Request to join' }))

    const steps = await screen.findByRole('list', { name: 'Request status' })
    expect(within(steps).getByText('Approved').closest('li')).toHaveAttribute('aria-current', 'step')
    expect(api.calls.find((c) => c.method === 'POST')?.body).toEqual({ message: 'Hi!' })

    await userEvent.click(screen.getByRole('button', { name: 'Withdraw request' }))
    expect(await screen.findByText('You withdrew your request.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Request to join' })).toBeInTheDocument()
  })

  it('asks for a groups-only plan with people from my trip', async () => {
    const api = mockApi({
      'GET /plans/plan-1': { ...publicPlan, audience: 'groupsOnly' },
      'GET /cards': [myTripTile],
      'GET /cards/card-mine': myTrip,
      'POST /plans/plan-1/requests': { ...publicPlan, myRequest: { id: 'pr-1', status: 'requested', partySize: 2, createdAt: '2026-10-06T10:00:00Z' } },
    })
    renderAt('/plans/plan-1')
    await userEvent.click(await screen.findByRole('button', { name: 'Request to join' }))
    const sheet = screen.getByRole('dialog', { name: 'Request to join' })
    expect(within(sheet).getByRole('radio', { name: /Just me/ })).toBeDisabled()
    await userEvent.click(await within(sheet).findByRole('checkbox', { name: 'Lena' }))
    await userEvent.click(within(sheet).getByRole('button', { name: 'Request 2 seats' }))

    expect(await screen.findByRole('list', { name: 'Request status' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'POST')?.body).toEqual({ message: '', sourceCardId: 'card-mine', partyUserIds: [lena.id] })
  })

  it('lets the host approve a request', async () => {
    const max = { ...lena, id: 'u-max', displayName: 'Max' }
    const api = mockApi({
      'GET /plans/plan-1': { ...hikePlan, pendingRequestCount: 1 },
      'GET /plans/plan-1/requests': [
        { id: 'pr-1', requester: max, party: [{ ...lena, id: 'u-ana', displayName: 'Ana' }], sourceCardName: 'Pai Pals', message: 'Two of us!', status: 'requested', createdAt: '2026-10-06T10:00:00Z' },
      ],
      'POST /plan-requests/pr-1/approve': { ...hikePlan, seatsTaken: 4, pendingRequestCount: 0 },
    })
    renderAt('/plans/plan-1')
    expect(await screen.findByText('Two of us!')).toBeInTheDocument()
    expect(screen.getByText('With Ana from Pai Pals')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    expect(api.calls.some((c) => c.method === 'POST' && c.path === '/plan-requests/pr-1/approve')).toBe(true)
    expect(await screen.findByText(/4 going/)).toBeInTheDocument()
  })

  it('offers to ask again after a declined request', async () => {
    mockApi({ 'GET /plans/plan-1': { ...publicPlan, myRequest: { id: 'pr-1', status: 'rejected', partySize: 1, createdAt: '2026-10-06T10:00:00Z' } } })
    renderAt('/plans/plan-1')
    expect(await screen.findByText("This request wasn't approved. You can ask again.")).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ask again' })).toBeInTheDocument()
  })
})
