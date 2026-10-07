import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import type { Privacy } from '../lib/types'
import { mockApi, reply, testUser } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

const privacy: Privacy = {
  legalVersion: '2026-10-01',
  needsConsent: false,
  analytics: false,
  marketingEmail: false,
  history: [{ kind: 'terms', version: '2026-10-01', grantedAt: '2026-10-01T09:00:00Z', withdrawnAt: null }],
}

const settings = { requests: true, messages: true, matches: true, reminders: true, reviews: true, email: true, quietFrom: null, quietTo: null, timeZone: 'UTC' }

describe('privacy', () => {
  it('shows the legal documents to visitors', async () => {
    mockApi({ 'GET /auth/me': { user: null } })
    renderAt('/legal/privacy')

    expect(await screen.findByRole('heading', { level: 1, name: 'Privacy Policy' })).toBeInTheDocument()
    expect(screen.getByText('Draft pending legal review.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Deleting your account' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Terms of Service' })).toHaveAttribute('href', '/legal/terms')
  })

  it('asks for consent again when the terms changed', async () => {
    const api = mockApi({
      'GET /auth/me': { user: testUser, needsConsent: true },
      'POST /me/consents/legal': { ...privacy },
      'GET /notifications/settings': settings,
    })
    renderAt('/settings')

    expect(await screen.findByRole('heading', { name: 'We updated our terms' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'I agree' }))

    expect(api.calls.some((c) => c.method === 'POST' && c.path === '/me/consents/legal')).toBe(true)
    expect(await screen.findByRole('heading', { name: 'Settings' })).toBeInTheDocument()
  })

  it('turns analytics on and links the data download', async () => {
    const api = mockApi({
      'GET /me/privacy': privacy,
      'PUT /me/consents': (body: unknown) => ({ ...privacy, analytics: (body as { granted: boolean }).granted }),
    })
    renderAt('/settings/privacy')

    const toggle = await screen.findByRole('switch', { name: /Help improve Travether/ })
    expect(toggle).not.toBeChecked()
    await userEvent.click(toggle)

    expect(api.calls.find((c) => c.method === 'PUT')?.body).toEqual({ kind: 'analytics', granted: true })
    expect(await screen.findByRole('switch', { name: /Help improve Travether/ })).toBeChecked()
    expect(screen.getByRole('link', { name: 'Download my data' })).toHaveAttribute('href', '/api/me/export')
  })

  it('deletes the account with the password and says goodbye', async () => {
    let deleted = false
    const api = mockApi({
      'GET /auth/me': () => ({ user: deleted ? null : testUser }),
      'GET /notifications/settings': settings,
      'POST /me/delete': () => {
        deleted = true
        return reply(204)
      },
    })
    renderAt('/settings')

    await userEvent.click(await screen.findByRole('button', { name: 'Delete account' }))
    const sheet = screen.getByRole('dialog', { name: 'Delete your account?' })
    await userEvent.click(within(sheet).getByRole('button', { name: 'Continue' }))
    await userEvent.type(within(sheet).getByLabelText('Password'), 'correct horse battery')
    await userEvent.click(within(sheet).getByRole('button', { name: 'Delete permanently' }))

    expect(api.calls.find((c) => c.path === '/me/delete')?.body).toEqual({ password: 'correct horse battery' })
    expect(await screen.findByText('Your account was deleted. Safe travels.')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login')
  })

  it('confirms with an emailed code when there is no password', async () => {
    const api = mockApi({
      'GET /auth/me': { user: { ...testUser, hasPassword: false } },
      'GET /notifications/settings': settings,
      'POST /me/delete/code': reply(202),
      'POST /me/delete': reply(204),
    })
    renderAt('/settings')

    await userEvent.click(await screen.findByRole('button', { name: 'Delete account' }))
    const sheet = screen.getByRole('dialog', { name: 'Delete your account?' })
    await userEvent.click(within(sheet).getByRole('button', { name: 'Continue' }))
    expect(within(sheet).getByRole('button', { name: 'Delete permanently' })).toBeDisabled()
    await userEvent.click(within(sheet).getByRole('button', { name: 'Email me a code' }))
    await userEvent.type(await within(sheet).findByLabelText('Code from the email'), '123456')
    await userEvent.click(within(sheet).getByRole('button', { name: 'Delete permanently' }))

    expect(api.calls.find((c) => c.path === '/me/delete')?.body).toEqual({ code: '123456' })
  })
})
