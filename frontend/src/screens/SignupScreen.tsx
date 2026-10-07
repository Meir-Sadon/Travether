import { useMemo, useState, type FormEvent } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { AccountStep } from '../auth/AccountStep'
import { safeNext, useAuth } from '../auth/useAuth'
import type { AuthResult, Me } from '../auth/types'
import { Button, Checkbox, Chip, FormError, SelectField, Stepper, TextField } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { track } from '../lib/telemetry'
import { countryOptions, interestTags, languageCodes, languageName } from '../lib/countries'

type NeedsProfile = Extract<AuthResult, { status: 'needsProfile' }>
type Credentials = { signupToken: string } | { email: string; password: string }

/** Latest date of birth that is 18 today, as yyyy-mm-dd for the date input's max. */
function adultCutoff(): string {
  const d = new Date()
  d.setFullYear(d.getFullYear() - 18)
  return d.toISOString().slice(0, 10)
}

function isAdult(dob: string): boolean {
  return dob !== '' && dob <= adultCutoff()
}

/** 2 · Sign up / onboarding in 3 steps, under a minute (PLAN.md §4.1). The 18+ rule is enforced by the API too. */
export function SignupScreen() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))
  const { setUser } = useAuth()

  // Log-in hands over a verified identity that has no account yet.
  const handedOver = (location.state as { signup?: NeedsProfile } | null)?.signup
  const [step, setStep] = useState(handedOver ? 1 : 0)
  const [credentials, setCredentials] = useState<Credentials | null>(handedOver ? { signupToken: handedOver.signupToken } : null)
  const [firstName, setFirstName] = useState(handedOver?.suggestedName ?? '')
  const [lastName, setLastName] = useState('')
  const [dob, setDob] = useState('')
  const [country, setCountry] = useState('')
  const [terms, setTerms] = useState(false)
  const [analytics, setAnalytics] = useState(false)
  const [interests, setInterests] = useState<string[]>([])
  const [languages, setLanguages] = useState<string[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const countries = useMemo(() => countryOptions(i18n.language), [i18n.language])
  const steps = [t('signup.stepAccount'), t('signup.stepAbout'), t('signup.stepYou')]

  const signedIn = (user: Me) => {
    setUser(user)
    void navigate(next, { replace: true })
  }

  const needsProfile = (r: NeedsProfile) => {
    setCredentials({ signupToken: r.signupToken })
    if (r.suggestedName) setFirstName(r.suggestedName)
    setStep(1)
  }

  const register = async (e: FormEvent) => {
    e.preventDefault()
    if (!isAdult(dob)) {
      setError('Underage')
      return
    }
    if (!terms) {
      setError('TermsRequired')
      return
    }
    setBusy(true)
    setError(null)
    try {
      const result = await api.post<AuthResult>('/auth/register', {
        ...credentials,
        displayName: firstName.trim(),
        fullName: `${firstName.trim()} ${lastName.trim()}`.trim(),
        dateOfBirth: dob,
        countryCode: country,
        acceptTerms: terms,
        allowAnalytics: analytics,
      })
      if (result.status === 'signedIn') {
        track('signed_up')
        setUser(result.user)
      }
      setStep(2)
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setBusy(false)
    }
  }

  const finish = async () => {
    setBusy(true)
    setError(null)
    try {
      const me = await api.patch<Me>('/me', { interests, languages })
      signedIn(me)
    } catch (err) {
      setError(errorCode(err))
      setBusy(false)
    }
  }

  const toggle = (list: string[], set: (v: string[]) => void, value: string, on: boolean) =>
    set(on ? [...list, value] : list.filter((x) => x !== value))

  const errorBox = <FormError code={error} />

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back={step !== 2} backTo="/welcome" title={t('signup.title')} />
      <div className="screen__section">
        <Stepper label={t('signup.progress')} steps={steps} current={step} />
      </div>

      {step === 0 && (
        <section className="screen__section">
          <h2>{t('signup.createAccount')}</h2>
          <p className="screen__meta">{t('signup.noId')}</p>
          <AccountStep
            mode="signup"
            onSignedIn={signedIn}
            onNeedsProfile={needsProfile}
            onPasswordChosen={(email, password) => {
              setCredentials({ email, password })
              setStep(1)
            }}
          />
          <p className="screen__meta" style={{ textAlign: 'center' }}>
            {t('auth.haveAccount')} <Link to={`/login?next=${encodeURIComponent(next)}`}>{t('auth.logIn')}</Link>
          </p>
        </section>
      )}

      {step === 1 && (
        <form className="screen" onSubmit={(e) => void register(e)} noValidate>
          <section className="screen__section">
            <h2>{t('signup.aboutYou')}</h2>
            <p className="screen__meta">{t('signup.aboutHint')}</p>
            <TextField
              label={t('signup.firstName')}
              autoComplete="given-name"
              required
              maxLength={40}
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
            />
            <TextField
              label={t('signup.lastName')}
              autoComplete="family-name"
              maxLength={80}
              hint={t('signup.lastNameHint')}
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
            />
            <TextField
              label={t('signup.dob')}
              type="date"
              autoComplete="bday"
              required
              max={adultCutoff()}
              hint={t('signup.dobHint')}
              value={dob}
              onChange={(e) => setDob(e.target.value)}
            />
            <SelectField
              label={t('signup.country')}
              required
              placeholder={t('signup.chooseCountry')}
              options={countries.map((c) => ({ value: c.code, label: c.name }))}
              value={country}
              onChange={(e) => setCountry(e.target.value)}
            />
            <Checkbox checked={terms} onChange={(e) => setTerms(e.target.checked)}>
              <Trans
                i18nKey="signup.acceptTerms"
                components={{ terms: <Link to="/legal/terms" />, guidelines: <Link to="/legal/guidelines" />, privacy: <Link to="/legal/privacy" /> }}
              />
            </Checkbox>
            <Checkbox checked={analytics} onChange={(e) => setAnalytics(e.target.checked)}>
              {t('signup.allowAnalytics')}
            </Checkbox>
            {errorBox}
          </section>
          <footer className="screen__footer">
            <Button type="submit" size="lg" block loading={busy} disabled={!firstName.trim() || !dob || !country}>
              {t('signup.continue')}
            </Button>
          </footer>
        </form>
      )}

      {step === 2 && (
        <>
          <section className="screen__section">
            <h2>{t('signup.makeItYours')}</h2>
            <p className="screen__meta">{t('signup.optional')}</p>
            <h3 className="screen__section-title">{t('signup.likes')}</h3>
            <div className="screen__row">
              {interestTags.map((i) => (
                <Chip key={i} size="md" selected={interests.includes(i)} onToggle={(on) => toggle(interests, setInterests, i, on)}>
                  {t(`interest.${i}`)}
                </Chip>
              ))}
            </div>
            <h3 className="screen__section-title">{t('signup.languages')}</h3>
            <div className="screen__row">
              {languageCodes.map((l) => (
                <Chip key={l} size="md" selected={languages.includes(l)} onToggle={(on) => toggle(languages, setLanguages, l, on)}>
                  {languageName(l, i18n.language)}
                </Chip>
              ))}
            </div>
            {errorBox}
          </section>
          <footer className="screen__footer">
            <Button size="lg" block loading={busy} onClick={() => void finish()}>
              {t('signup.startExploring')}
            </Button>
            <Button variant="ghost" block onClick={() => void navigate(next, { replace: true })}>
              {t('signup.skip')}
            </Button>
          </footer>
        </>
      )}
    </div>
  )
}
