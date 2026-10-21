import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth'
import type { AuthResult } from '../auth/types'
import { Button, FormError, TextField } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'

/** Reset a password with an emailed code. Other sessions are signed out by the API. */
export function ForgotPasswordScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { setUser } = useAuth()
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const [sent, setSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      if (!sent) {
        await api.post('/auth/password/forgot', { email })
        setSent(true)
      } else {
        const result = await api.post<AuthResult>('/auth/password/reset', { email, code, newPassword: password })
        if (result.status === 'signedIn') setUser(result.user)
        void navigate('/', { replace: true })
      }
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back backTo="/login" title={t('auth.resetTitle')} />
      <form className="screen__section" onSubmit={(e) => void submit(e)} noValidate>
        <p className="screen__meta">{sent ? t('auth.resetSent', { email }) : t('auth.resetLead')}</p>
        <TextField label={t('signup.email')} type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
        {sent && (
          <>
            <TextField
              label={t('auth.code')}
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              required
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
            />
            <TextField
              label={t('auth.newPassword')}
              type="password"
              autoComplete="new-password"
              minLength={8}
              required
              hint={t('auth.passwordHint')}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
            />
          </>
        )}
        <FormError code={error} />
        <Button type="submit" size="lg" block loading={busy}>
          {sent ? t('auth.setPassword') : t('auth.sendResetCode')}
        </Button>
      </form>
    </div>
  )
}
