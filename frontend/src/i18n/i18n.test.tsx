import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { Stepper } from '../components'
import i18n, { applyDocumentLanguage } from '.'

describe('i18n', () => {
  afterEach(async () => {
    await i18n.changeLanguage('en')
  })

  it('serves English strings', () => {
    expect(i18n.t('app.tagline')).toBe('Find people to do things with on your trip.')
    expect(i18n.t('status.api', { state: 'ok' })).toBe('API: ok')
  })

  it('keeps <html lang dir> in sync with the language', async () => {
    await i18n.changeLanguage('en')
    expect(document.documentElement).toHaveAttribute('lang', 'en')
    expect(document.documentElement).toHaveAttribute('dir', 'ltr')
  })

  it('switches the document to rtl for Hebrew', () => {
    applyDocumentLanguage('he')
    expect(document.documentElement).toHaveAttribute('lang', 'he')
    expect(document.documentElement).toHaveAttribute('dir', 'rtl')
    applyDocumentLanguage('en')
  })

  it('falls back to English for languages without a translation yet', async () => {
    await i18n.changeLanguage('he')
    render(<Stepper label="Status" steps={['Requested', 'Approved']} current={1} />)
    expect(screen.getByText('(done)', { exact: false })).toBeInTheDocument()
  })
})
