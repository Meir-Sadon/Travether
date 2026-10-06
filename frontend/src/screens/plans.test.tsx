import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import { crewCard, crewTile, hikePlan, hikeSummary, lena, noa, publicPlan } from '../test/fixtures'
import { mockApi } from '../test/mockApi'

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
