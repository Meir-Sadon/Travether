import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it } from 'vitest'
import App from '../App'
import { mockApi } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('mockup screens', () => {
  beforeEach(() => {
    mockApi({ 'GET /auth/providers': { googleClientId: null, appleClientId: null, appleRedirectUri: null } })
  })

  it.each([
    ['/signup', 'Sign up'],
    ['/', 'Hi Noa'],
    ['/discover', 'Chiang Mai'],
    ['/inbox', 'Inbox'],
    ['/inbox/sanctuary', 'Elephant sanctuary'],
    ['/profile', 'Profile'],
    ['/settings', 'Settings'],
    ['/plans/sanctuary/review', /happen\?/],
    ['/nope', 'Nothing here'],
  ])('%s renders', async (path, heading) => {
    renderAt(path)
    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeInTheDocument()
  })

  it('shows the bottom navigation only on the main tabs', async () => {
    renderAt('/discover')
    expect(await screen.findByRole('navigation', { name: 'Main' })).toBeInTheDocument()
  })

  it('filters Discover by category', async () => {
    renderAt('/discover')
    await userEvent.click(await screen.findByRole('button', { name: 'Food' }))
    expect(screen.getByText('Khao soi tasting tour')).toBeInTheDocument()
    expect(screen.queryByText('Mae Sa waterfall trail')).not.toBeInTheDocument()
  })

  it('sends a chat message', async () => {
    renderAt('/inbox/sanctuary')
    await userEvent.type(await screen.findByRole('textbox', { name: 'Message' }), 'On my way{Enter}')
    expect(screen.getByText('On my way')).toBeInTheDocument()
  })
})
