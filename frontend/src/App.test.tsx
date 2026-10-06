import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

describe('App', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the API health status', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response(JSON.stringify({ status: 'ok', database: 'ok' }))),
    )
    render(<App />)
    expect(screen.getByRole('heading', { name: 'Travether' })).toBeInTheDocument()
    expect(await screen.findByText('API: ok')).toBeInTheDocument()
  })

  it('reports an unreachable API', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('', { status: 503 })))
    render(<App />)
    expect(await screen.findByText('API: unreachable')).toBeInTheDocument()
  })
})
