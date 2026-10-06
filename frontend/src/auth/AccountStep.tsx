import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button, FormError, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import { renderGoogleButton, signInWithApple } from './externalSignIn'
import type { AuthResult, Me, Providers } from './types'

type AccountStepProps = {
  mode: 'login' | 'signup'
  onSignedIn: (user: Me) => void
  onNeedsProfile: (result: Extract<AuthResult, { status: 'needsProfile' }>) => void
  /** Sign-up only: email + new password, created together with the profile in the next step. */
  onPasswordChosen?: (email: string, password: string) => void
}

type Method = 'code' | 'password'

/**
 * The account part of log-in and sign-up: Google, Apple, an emailed one-time code, or a password.
 * Existing accounts sign in; new identities continue to the profile step with a sign-up token.
 */
export function AccountStep({ mode, onSignedIn, onNeedsProfile, onPasswordChosen }: AccountStepProps) {
  const { t, i18n } = useTranslation()
  const [providers, setProviders] = useState<Providers | null>(null)
  const [method, setMethod] = useState<Method>('code')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [codeSent, setCodeSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const googleRef = useRef<HTMLDivElement>(null)

  const handle = (result: AuthResult) => (result.status === 'signedIn' ? onSignedIn(result.user) : onNeedsProfile(result))

  const run = async (work: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    try {
      await work()
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  useEffect(() => {
    const ctrl = new AbortController()
    api
      .get<Providers>('/auth/providers', ctrl.signal)
      .then(setProviders)
      .catch(() => setProviders({ googleClientId: null, appleClientId: null, appleRedirectUri: null }))
    return () => ctrl.abort()
  }, [])

  // Google calls back long after render; the ref always holds the latest handler.
  const onGoogleToken = useRef<(idToken: string) => void>(() => {})
  useEffect(() => {
    onGoogleToken.current = (idToken) =>
      void run(async () => handle(await api.post<AuthResult>('/auth/external', { provider: 'google', idToken })))
  })

  const googleClientId = providers?.googleClientId
  const language = i18n.language
  useEffect(() => {
    if (!googleClientId || !googleRef.current) return
    renderGoogleButton(googleRef.current, googleClientId, (token) => onGoogleToken.current(token), language).catch(() =>
      setError('ProviderUnavailable'),
    )
  }, [googleClientId, language])

  const apple = () =>
    run(async () => {
      const { idToken, givenName } = await signInWithApple(providers!.appleClientId!, providers!.appleRedirectUri ?? window.location.origin)
      handle(await api.post<AuthResult>('/auth/external', { provider: 'apple', idToken, givenName }))
    })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (method === 'password') {
      if (mode === 'signup') {
        onPasswordChosen?.(email.trim(), password)
        return
      }
      void run(async () => handle(await api.post<AuthResult>('/auth/login', { email, password })))
      return
    }
    if (!codeSent) {
      void run(async () => {
        await api.post('/auth/email/start', { email })
        setCodeSent(true)
      })
      return
    }
    void run(async () => handle(await api.post<AuthResult>('/auth/email/verify', { email, code })))
  }

  const switchMethod = () => {
    setMethod(method === 'code' ? 'password' : 'code')
    setCodeSent(false)
    setError(null)
  }

  const submitLabel =
    method === 'password'
      ? mode === 'signup'
        ? t('signup.continue')
        : t('auth.logIn')
      : codeSent
        ? t('auth.verifyCode')
        : t('signup.sendCode')

  return (
    <form className="screen__stack" onSubmit={submit} noValidate>
      {providers?.googleClientId && <div ref={googleRef} className="auth__google" />}
      {providers?.appleClientId && (
        <Button variant="secondary" block onClick={() => void apple()} disabled={busy}>
          {t('signup.apple')}
        </Button>
      )}
      {(providers?.googleClientId || providers?.appleClientId) && (
        <p className="screen__meta" style={{ textAlign: 'center' }}>
          {t('signup.or')}
        </p>
      )}

      <TextField
        label={t('signup.email')}
        type="email"
        autoComplete="email"
        required
        value={email}
        onChange={(e) => {
          setEmail(e.target.value)
          setCodeSent(false)
        }}
        placeholder="noa@example.com"
      />

      {method === 'password' && (
        <TextField
          label={t('auth.password')}
          type="password"
          autoComplete={mode === 'signup' ? 'new-password' : 'current-password'}
          required
          minLength={8}
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          hint={mode === 'signup' ? t('auth.passwordHint') : undefined}
        />
      )}

      {method === 'code' && codeSent && (
        <TextField
          label={t('auth.code')}
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="\d{6}"
          maxLength={6}
          required
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
          hint={t('auth.codeSent', { email })}
        />
      )}

      <FormError code={error} />

      <Button type="submit" size="lg" block loading={busy}>
        {submitLabel}
      </Button>

      <Button variant="ghost" block onClick={switchMethod}>
        {method === 'code' ? (mode === 'signup' ? t('auth.usePasswordSignup') : t('auth.usePassword')) : t('auth.useCode')}
      </Button>

      {mode === 'login' && method === 'password' && (
        <Link to="/forgot" className="auth__link">
          {t('auth.forgot')}
        </Link>
      )}
    </form>
  )
}
