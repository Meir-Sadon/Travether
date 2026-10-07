import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { useMe } from '../auth/useAuth'
import { BottomSheet, Button, FormError, TextField } from '../components'
import { api, errorCode } from '../lib/api'

type Props = { open: boolean; onClose: () => void }

/**
 * Deletes the account (PLAN.md §4.9): a warning, then the password or an emailed code to confirm.
 * Accounts made with Google, Apple or codes have no password and always use a code.
 */
export function DeleteAccountSheet({ open, onClose }: Props) {
  const { t } = useTranslation()
  const me = useMe()
  const navigate = useNavigate()
  const [step, setStep] = useState<'warn' | 'confirm'>('warn')
  const [useCode, setUseCode] = useState(!me.hasPassword)
  const [codeSent, setCodeSent] = useState(false)
  const [password, setPassword] = useState('')
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const close = () => {
    setStep('warn')
    setPassword('')
    setCode('')
    setError(null)
    onClose()
  }

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

  const sendCode = () =>
    run(async () => {
      await api.post('/me/delete/code')
      setCodeSent(true)
    })

  const submit = (e: FormEvent) => {
    e.preventDefault()
    void run(async () => {
      await api.post('/me/delete', useCode ? { code } : { password })
      // The landing page reloads the (now empty) session; clearing it here would let the sign-in guard redirect first.
      void navigate('/welcome?deleted=1', { replace: true })
    })
  }

  return (
    <BottomSheet
      open={open}
      onClose={close}
      title={t('settings.deleteTitle')}
      footer={
        step === 'warn' ? (
          <div className="screen__stack">
            <Button block size="lg" variant="brand" onClick={close}>
              {t('settings.keepAccount')}
            </Button>
            <Button block variant="ghost" onClick={() => setStep('confirm')}>
              {t('settings.deleteContinue')}
            </Button>
          </div>
        ) : undefined
      }
    >
      {step === 'warn' ? (
        <div className="screen__stack">
          <p className="screen__body">{t('settings.deleteBody')}</p>
          <p className="screen__meta">{t('settings.deleteExportHint')}</p>
        </div>
      ) : (
        <form className="screen__stack" onSubmit={submit} noValidate>
          {useCode ? (
            <>
              <p className="screen__body">{t('settings.deleteCodeBody', { email: me.email })}</p>
              {codeSent ? (
                <TextField
                  label={t('settings.deleteCode')}
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={code}
                  onChange={(e) => setCode(e.target.value)}
                />
              ) : (
                <Button block variant="secondary" disabled={busy} onClick={() => void sendCode()}>
                  {t('settings.deleteSendCode')}
                </Button>
              )}
            </>
          ) : (
            <>
              <p className="screen__body">{t('settings.deletePasswordBody')}</p>
              <TextField
                label={t('auth.password')}
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <button type="button" className="screen__link" onClick={() => setUseCode(true)}>
                {t('settings.deleteUseCode')}
              </button>
            </>
          )}
          <FormError code={error} />
          <Button
            type="submit"
            block
            size="lg"
            variant="primary"
            disabled={busy || (useCode ? !codeSent || code.trim().length !== 6 : password.length === 0)}
          >
            {t('settings.deleteConfirm')}
          </Button>
          <Button block variant="ghost" onClick={close}>
            {t('settings.keepAccount')}
          </Button>
        </form>
      )}
    </BottomSheet>
  )
}
