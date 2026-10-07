import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import { mockApi, reply, testUser } from '../test/mockApi'

const noProviders = { 'GET /auth/providers': { googleClientId: null, appleClientId: null, appleRedirectUri: null } }

function Where() {
  const loc = useLocation()
  return <output data-testid="where">{loc.pathname + loc.search}</output>
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
      <Where />
    </MemoryRouter>,
  )
}

describe('auth', () => {
  it('sends visitors from a private page to the landing page and remembers where they were going', async () => {
    mockApi({ 'GET /auth/me': { user: null } })
    renderAt('/inbox')
    expect(await screen.findByRole('heading', { level: 1, name: /Find people to do things with/ })).toBeInTheDocument()
    expect(screen.getByTestId('where')).toHaveTextContent('/welcome?next=%2Finbox')
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login?next=%2Finbox')
  })

  it('lets visitors browse Discover without an account', async () => {
    mockApi({ 'GET /auth/me': { user: null } })
    renderAt('/discover')
    expect(await screen.findByRole('heading', { level: 1, name: 'Discover' })).toBeInTheDocument()
  })

  it('signs up with an emailed code in three steps', async () => {
    const api = mockApi({
      ...noProviders,
      'GET /auth/me': { user: null },
      'POST /auth/email/start': undefined,
      'POST /auth/email/verify': { status: 'needsProfile', signupToken: 'tok', email: 'noa@example.com', suggestedName: null },
      'POST /auth/register': { status: 'signedIn', user: testUser },
      'PATCH /me': { ...testUser, interests: ['food'] },
    })
    renderAt('/signup')

    await userEvent.type(await screen.findByLabelText('Email'), 'noa@example.com')
    await userEvent.click(screen.getByRole('button', { name: 'Send me a code' }))
    await userEvent.type(await screen.findByLabelText('6-digit code'), '123456')
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    await userEvent.type(await screen.findByLabelText('First name'), 'Noa')
    await userEvent.type(screen.getByLabelText('Last name'), 'Levi')
    await userEvent.type(screen.getByLabelText('Date of birth'), '1996-04-02')
    await userEvent.selectOptions(screen.getByLabelText('Country of origin'), 'IL')
    await userEvent.click(screen.getByRole('checkbox', { name: /I'm 18 or older/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    await userEvent.click(await screen.findByRole('button', { name: 'Food' }))
    await userEvent.click(screen.getByRole('button', { name: 'Start exploring' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Hi Noa' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.path === '/auth/register')?.body).toEqual({
      signupToken: 'tok',
      displayName: 'Noa',
      fullName: 'Noa Levi',
      dateOfBirth: '1996-04-02',
      countryCode: 'IL',
      acceptTerms: true,
      allowAnalytics: false,
    })
    expect(api.calls.find((c) => c.path === '/me')?.body).toEqual({ interests: ['food'], languages: [] })
  })

  it('stops under-18s before calling the API', async () => {
    const api = mockApi({ ...noProviders, 'GET /auth/me': { user: null } })
    renderAt('/signup')
    await userEvent.click(await screen.findByRole('button', { name: 'Create a password instead' }))
    await userEvent.type(screen.getByLabelText('Email'), 'kid@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'long enough pw')
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    await userEvent.type(await screen.findByLabelText('First name'), 'Kid')
    const thisYear = new Date().getFullYear()
    await userEvent.type(screen.getByLabelText('Date of birth'), `${thisYear - 10}-01-01`)
    await userEvent.selectOptions(screen.getByLabelText('Country of origin'), 'IL')
    await userEvent.click(screen.getByRole('checkbox', { name: /I'm 18 or older/ }))
    await userEvent.click(screen.getByRole('button', { name: 'Continue' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('You must be 18 or older')
    expect(api.calls.some((c) => c.path === '/auth/register')).toBe(false)
  })

  it('shows the API error when a password log-in fails', async () => {
    mockApi({
      ...noProviders,
      'GET /auth/me': { user: null },
      'POST /auth/login': reply(401, { code: 'InvalidCredentials' }),
    })
    renderAt('/login')
    await userEvent.click(await screen.findByRole('button', { name: 'Use a password instead' }))
    await userEvent.type(screen.getByLabelText('Email'), 'noa@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'wrong password')
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
  })

  it('logs in and returns to the page the visitor asked for', async () => {
    mockApi({
      ...noProviders,
      'GET /auth/me': { user: null },
      'POST /auth/login': { status: 'signedIn', user: testUser },
    })
    renderAt('/login?next=%2Fprofile')
    await userEvent.click(await screen.findByRole('button', { name: 'Use a password instead' }))
    await userEvent.type(screen.getByLabelText('Email'), 'noa@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct horse')
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Profile' })).toBeInTheDocument()
  })

  it('ignores a next= that points to another site', async () => {
    mockApi({ ...noProviders, 'GET /auth/me': { user: null }, 'POST /auth/login': { status: 'signedIn', user: testUser } })
    renderAt('/login?next=%2F%2Fevil.example')
    await userEvent.click(await screen.findByRole('button', { name: 'Use a password instead' }))
    await userEvent.type(screen.getByLabelText('Email'), 'noa@example.com')
    await userEvent.type(screen.getByLabelText('Password'), 'correct horse')
    await userEvent.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Hi Noa' })).toBeInTheDocument()
    expect(screen.getByTestId('where')).toHaveTextContent(/^\/$/)
  })

  it('ignores a next= that a browser would read as another site', async () => {
    const { safeNext } = await import('./useAuth')
    expect(safeNext('/\\evil.example')).toBe('/')
    expect(safeNext('/trips/1?tab=plans')).toBe('/trips/1?tab=plans')
  })
})
