import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  it('shows the API and database status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'ok', database: 'ok' }))),
    )
    renderAt('/')
    expect(screen.getByRole('heading', { name: 'Travether' })).toBeInTheDocument()
    expect(await screen.findByText('API: ok · database: ok')).toBeInTheDocument()
  })

  it('reports an unreachable API', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))
    renderAt('/')
    expect(await screen.findByText('API: unreachable')).toBeInTheDocument()
  })

  it('renders the design system page', () => {
    renderAt('/design')
    expect(screen.getByRole('heading', { name: 'Travether design system' })).toBeInTheDocument()
  })
})
