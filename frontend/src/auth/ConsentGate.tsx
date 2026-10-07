import { useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button, FormError } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { errorCode } from '../lib/api'
import { useAuth } from './useAuth'

/** Shown instead of the app when the legal documents changed since the user last accepted them. */
export function ConsentGate() {
  const { t } = useTranslation()
  const { acceptConsent, logout } = useAuth()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const accept = async () => {
    setBusy(true)
    setError(null)
    try {
      await acceptConsent()
    } catch (err) {
      setError(errorCode(err))
      setBusy(false)
    }
  }

  return (
    <div className="screen">
      <ScreenHeader large title={t('legal.updatedTitle')} />
      <section className="screen__section screen__stack">
        <p className="screen__body">
          <Trans
            i18nKey="legal.updatedBody"
            components={{ terms: <Link to="/legal/terms" />, guidelines: <Link to="/legal/guidelines" />, privacy: <Link to="/legal/privacy" /> }}
          />
        </p>
        <FormError code={error} />
        <Button block size="lg" variant="brand" disabled={busy} onClick={() => void accept()}>
          {t('legal.accept')}
        </Button>
        <Button block variant="ghost" onClick={() => void logout()}>
          {t('settings.logOut')}
        </Button>
      </section>
    </div>
  )
}
