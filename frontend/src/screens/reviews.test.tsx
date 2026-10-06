import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from '../App'
import type { Review, WrapUp } from '../lib/types'
import { lena, noa } from '../test/fixtures'
import { mockApi, testUser } from '../test/mockApi'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

const planId = 'p-1'
const unanswered: WrapUp = {
  planId,
  title: 'Sunrise hike',
  category: 'hike',
  localDate: '2026-10-05',
  myAnswer: null,
  canAnswer: true,
  answerUntil: '2026-10-19T00:00:00Z',
  people: [{ person: lena, state: 'waiting', reviewUntil: null, myReview: null, theyReviewedMe: true, theirReview: null }],
}
const lenaReview: Review = {
  id: 'r-1',
  reviewer: lena,
  stars: 5,
  text: 'Great pace!',
  planId,
  planTitle: 'Sunrise hike',
  category: 'hike',
  createdAt: '2026-10-06T10:00:00Z',
  reply: null,
  repliedAt: null,
}

describe('reviews', () => {
  it('asks "Did you meet?", then takes a review that stays blind', async () => {
    const open: WrapUp = { ...unanswered, myAnswer: 'met', people: [{ ...unanswered.people[0], state: 'open', reviewUntil: '2026-10-20T00:00:00Z' }] }
    const api = mockApi({
      [`GET /plans/${planId}/wrap-up`]: unanswered,
      [`POST /plans/${planId}/meet`]: open,
      [`POST /plans/${planId}/reviews`]: {
        ...open,
        people: [{ ...open.people[0], state: 'reviewed', reviewUntil: null, myReview: { ...lenaReview, id: 'r-2', reviewer: noa, stars: 4, text: 'Lovely' } }],
      },
    })
    renderAt(`/plans/${planId}/review`)

    await userEvent.click(await screen.findByRole('button', { name: 'Yes, we met' }))
    expect(api.calls.find((c) => c.path.endsWith('/meet'))?.body).toEqual({ answer: 'met' })

    const lenaSection = await screen.findByRole('region', { name: 'Lena' })
    expect(within(lenaSection).getByText('Lena reviewed you. Leave your review to see theirs.')).toBeInTheDocument()
    await userEvent.click(within(lenaSection).getByRole('radio', { name: '4 stars' }))
    await userEvent.type(within(lenaSection).getByRole('textbox'), 'Lovely')
    await userEvent.click(within(lenaSection).getByRole('button', { name: 'Send review for Lena' }))

    expect(api.calls.find((c) => c.path.endsWith('/reviews'))?.body).toEqual({ revieweeId: lena.id, stars: 4, text: 'Lovely' })
    expect(await within(lenaSection).findByText('Your review')).toBeInTheDocument()
  })

  it('shows reviews on my profile and lets me reply once', async () => {
    const api = mockApi({
      [`GET /users/${testUser.id}`]: { id: testUser.id, displayName: 'Noa', rating: { average: null, count: 1 } },
      [`GET /users/${testUser.id}/reviews`]: { items: [lenaReview], hasMore: false },
      'POST /reviews/r-1/reply': { ...lenaReview, reply: 'Thank you!', repliedAt: '2026-10-07T10:00:00Z' },
    })
    renderAt('/profile')

    expect(await screen.findByText('Great pace!')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Reply publicly' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Your reply' }), 'Thank you!')
    await userEvent.click(screen.getByRole('button', { name: 'Post reply' }))

    expect(api.calls.find((c) => c.path === '/reviews/r-1/reply')?.body).toEqual({ text: 'Thank you!' })
    expect(await screen.findByText('Thank you!')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reply publicly' })).not.toBeInTheDocument()
  })

  it('reminds me on Home about plans to wrap up', async () => {
    mockApi({
      'GET /cards': [],
      'GET /me/wrap-ups': [{ planId, title: 'Sunrise hike', category: 'hike', localDate: '2026-10-05', needsAnswer: true, toReview: 0 }],
    })
    renderAt('/')

    expect(await screen.findByRole('link', { name: 'Did Sunrise hike happen?' })).toHaveAttribute('href', `/plans/${planId}/review`)
  })
})
