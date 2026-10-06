import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('mockup screens', () => {
  it.each([
    ['/welcome', /Find people to do things with/],
    ['/signup', 'Sign up'],
    ['/', 'Hi Noa'],
    ['/discover', 'Chiang Mai'],
    ['/plans/sanctuary', 'Elephant sanctuary + waterfall'],
    ['/trips/cm-crew', 'Chiang Mai Crew'],
    ['/c/cm-crew-7K2', 'Chiang Mai Crew'],
    ['/inbox', 'Inbox'],
    ['/inbox/sanctuary', 'Elephant sanctuary'],
    ['/profile', 'Profile'],
    ['/settings', 'Settings'],
    ['/plans/sanctuary/review', /happen\?/],
    ['/nope', 'Nothing here'],
  ])('%s renders', (path, heading) => {
    renderAt(path)
    expect(screen.getByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it('shows the bottom navigation only on the main tabs', () => {
    renderAt('/discover')
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument()
  })

  it('reveals the exact meeting point only after approval', async () => {
    renderAt('/plans/sanctuary')
    expect(screen.queryByText(/7-Eleven/)).not.toBeInTheDocument()
    expect(screen.getByText(/Exact meeting point shown once you're approved/)).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: 'Request to join' }))
    const status = screen.getByRole('list', { name: 'Request status' })
    expect(within(status).getByText('Approved').closest('li')).toHaveAttribute('aria-current', 'step')

    await userEvent.click(screen.getByRole('button', { name: /simulate host approval/ }))
    expect(screen.getByText(/Meet: 7-Eleven, Huay Kaew Rd/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Open plan chat' })).toHaveAttribute('href', '/inbox/sanctuary')
  })

  it('lets the owner approve a join request', async () => {
    renderAt('/trips/cm-crew')
    await userEvent.click(screen.getByRole('radio', { name: 'Members · 3' }))
    await userEvent.click(screen.getByRole('button', { name: 'Approve' }))
    expect(screen.getByRole('radio', { name: 'Members · 4' })).toBeChecked()
  })

  it('filters Discover by category', async () => {
    renderAt('/discover')
    await userEvent.click(screen.getByRole('button', { name: 'Food' }))
    expect(screen.getByText('Khao soi tasting tour')).toBeInTheDocument()
    expect(screen.queryByText('Mae Sa waterfall trail')).not.toBeInTheDocument()
  })

  it('opens the create sheet from the + button', async () => {
    renderAt('/')
    await userEvent.click(screen.getByRole('button', { name: 'Create a plan or trip' }))
    await userEvent.click(screen.getByRole('button', { name: /New Activity Plan/ }))
    expect(screen.getByRole('dialog', { name: 'New Activity Plan' })).toBeInTheDocument()
    expect(screen.getByText(/exact spot is shown only to approved participants/)).toBeInTheDocument()
  })

  it('sends a chat message', async () => {
    renderAt('/inbox/sanctuary')
    await userEvent.type(screen.getByRole('textbox', { name: 'Message' }), 'On my way{Enter}')
    expect(screen.getByText('On my way')).toBeInTheDocument()
  })
})
