import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { AccountStep } from '../auth/AccountStep'
import { safeNext, useAuth } from '../auth/useAuth'
import { ScreenHeader } from '../layout/ScreenHeader'

/** Log in with Google, Apple, an emailed code or a password. A new identity continues to sign-up. */
export function LoginScreen() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))
  const { setUser } = useAuth()

  return (
    <div className="screen" style={{ minBlockSize: '100dvh' }}>
      <ScreenHeader back backTo="/welcome" title={t('auth.logInTitle')} />
      <section className="screen__section">
        <p className="screen__meta">{t('auth.logInLead')}</p>
        <AccountStep
          mode="login"
          onSignedIn={(user) => {
            setUser(user)
            void navigate(next, { replace: true })
          }}
          onNeedsProfile={(signup) => void navigate(`/signup?next=${encodeURIComponent(next)}`, { state: { signup } })}
        />
        <p className="screen__meta" style={{ textAlign: 'center' }}>
          {t('auth.newHere')} <Link to={`/signup?next=${encodeURIComponent(next)}`}>{t('auth.createAccount')}</Link>
        </p>
      </section>
    </div>
  )
}
