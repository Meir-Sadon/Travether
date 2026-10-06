import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import { mockApi, testUser } from '../test/mockApi'

const publicNoa = {
  id: testUser.id,
  displayName: 'Noa',
  age: 30,
  countryCode: 'IL',
  photoUrl: null,
  bio: null,
  languages: [],
  interests: [],
  badges: ['contactVerified'],
  rating: { average: null, count: 2 },
  memberSince: '2026-09-01T10:00:00Z',
  fullName: null,
  email: null,
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('profile', () => {
  it('shows completeness and asks for the next missing thing', async () => {
    mockApi({ [`GET /users/${testUser.id}`]: publicNoa })
    renderAt('/profile')
    expect(await screen.findByText('Your profile is 75% complete')).toBeInTheDocument()
    expect(screen.getByText(/Add a photo so people recognise you/)).toBeInTheDocument()
    expect(await screen.findByText('2')).toBeInTheDocument() // review count, average hidden under 3
  })

  it('uploads a photo', async () => {
    const api = mockApi({
      [`GET /users/${testUser.id}`]: publicNoa,
      'POST /me/photo': { ...testUser, photoUrl: '/uploads/avatars/x.png', strength: { percent: 100, missing: [] } },
    })
    renderAt('/profile')
    const input = await screen.findByLabelText('Profile photo')
    await userEvent.upload(input, new File(['png'], 'me.png', { type: 'image/png' }))
    expect(await screen.findByRole('button', { name: 'Change photo' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.path === '/me/photo')?.body).toBeInstanceOf(FormData)
    expect(screen.queryByText(/% complete/)).not.toBeInTheDocument()
  })

  it('saves edits and returns to the profile', async () => {
    const api = mockApi({
      [`GET /users/${testUser.id}`]: publicNoa,
      'PATCH /me': (body: unknown) => ({ ...testUser, ...(body as object) }),
    })
    renderAt('/profile/edit')
    await userEvent.type(await screen.findByLabelText('About you'), 'Early riser.')
    await userEvent.type(screen.getByLabelText('Phone (optional)'), '+972 50-123 4567')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Profile' })).toBeInTheDocument()
    expect(api.calls.find((c) => c.method === 'PATCH')?.body).toMatchObject({ bio: 'Early riser.', phone: '+972501234567' })
  })

  it("shows another traveler's full name only when the API sends it", async () => {
    mockApi({ 'GET /users/u-lena': { ...publicNoa, id: 'u-lena', displayName: 'Lena', countryCode: 'DE', fullName: 'Lena Vogel' } })
    renderAt('/people/u-lena')
    expect(await screen.findByRole('heading', { level: 2, name: 'Lena, 30' })).toBeInTheDocument()
    expect(screen.getByText('Lena Vogel')).toBeInTheDocument()
    expect(screen.getByText('Germany')).toBeInTheDocument()
  })
})
