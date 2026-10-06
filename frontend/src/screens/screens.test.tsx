import { render, screen } from '@testing-library/react'
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
    ['/inbox', 'Inbox'],
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
})
