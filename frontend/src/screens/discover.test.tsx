import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it } from 'vitest'
import App from '../App'
import { crewTile, hikeSummary, lena } from '../test/fixtures'
import { mockApi } from '../test/mockApi'
import type { DiscoverPlan, Place } from '../lib/types'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

const chiangMai: Place = { name: 'Chiang Mai', area: 'Chiang Mai, Thailand', lat: 18.79, lng: 98.98 }
const trip = { ...crewTile, startsOn: '2999-01-10', endsOn: '2999-01-20' }
const found: DiscoverPlan[] = [
  { plan: { ...hikeSummary, host: lena, joined: false }, distance: { km: 2, underOneKm: false } },
  { plan: { ...hikeSummary, id: 'plan-2', title: 'Khao soi crawl', category: 'food', host: lena, joined: false }, distance: { km: 1, underOneKm: true } },
]

describe('discover', () => {
  beforeEach(() => localStorage.clear())

  it("searches around the trip's city on the trip's dates", async () => {
    const api = mockApi({ 'GET /cards': [trip], 'GET /places/search': [chiangMai], 'GET /discover': found })
    renderAt('/discover')

    expect(await screen.findByRole('heading', { level: 1, name: 'Chiang Mai' })).toBeInTheDocument()
    const hike = await screen.findByRole('link', { name: /Sunrise hike to Doi Suthep/ })
    expect(hike).toHaveAttribute('href', '/plans/plan-1?lat=18.79&lng=98.98')
    expect(within(hike).getByText('~2 km')).toBeInTheDocument()
    expect(screen.getByText('under 1 km')).toBeInTheDocument()
    expect(screen.getByText('2 plans · nearest first')).toBeInTheDocument()

    const search = api.calls.find((c) => c.path === '/discover')!.query
    expect(Object.fromEntries(search)).toEqual({ lat: '18.79', lng: '98.98', from: '2999-01-10', to: '2999-01-20', radiusKm: '30', sort: 'distance' })
    expect(api.calls.find((c) => c.path === '/places/search')!.query.get('q')).toBe('Chiang Mai, Thailand')
  })

  it('filters by category and group size', async () => {
    const api = mockApi({ 'GET /cards': [trip], 'GET /places/search': [chiangMai], 'GET /discover': found })
    renderAt('/discover')
    await screen.findByText('2 plans · nearest first')

    await userEvent.click(screen.getByRole('button', { name: 'Food' }))
    await userEvent.click(screen.getByRole('button', { name: 'Filters' }))
    await userEvent.click(within(screen.getByRole('dialog', { name: 'Filters' })).getByRole('button', { name: 'Up to 4' }))

    const last = api.calls.filter((c) => c.path === '/discover').at(-1)!.query
    expect(last.get('category')).toBe('food')
    expect(last.get('maxSeats')).toBe('4')
  })

  it('lets visitors pick a place to browse', async () => {
    mockApi({ 'GET /auth/me': { user: null }, 'GET /places/search': [chiangMai], 'GET /discover': found })
    renderAt('/discover')
    expect(await screen.findByText('Choose where you are to see plans nearby.')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Choose where you are' }))
    const sheet = screen.getByRole('dialog', { name: 'Search from' })
    await userEvent.type(within(sheet).getByLabelText('Place'), 'Chiang')
    await userEvent.click(await within(sheet).findByRole('button', { name: /Chiang Mai/ }))

    expect(await screen.findByRole('link', { name: /Khao soi crawl/ })).toBeInTheDocument()
  })

  it('shows trip matches on Home', async () => {
    mockApi({ 'GET /cards': [trip], 'GET /places/search': [chiangMai], 'GET /discover': found })
    renderAt('/')
    expect(await screen.findByRole('heading', { name: 'Matches for your dates' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Khao soi crawl/ })).toHaveAttribute('href', '/plans/plan-2?lat=18.79&lng=98.98')
  })
})
