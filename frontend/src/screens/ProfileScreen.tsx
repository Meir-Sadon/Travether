import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { Me } from '../auth/types'
import { useAuth, useMe } from '../auth/useAuth'
import { Button, Card, CardBody, FormError, Icon, TextField } from '../components'
import { ProfileView } from '../features/ProfileView'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import type { PublicProfile } from '../lib/types'
import { useApi } from '../lib/useApi'
import './ProfileScreen.css'

/** 8 · Your profile: badges, rating (3+ reviews), and a nudge to fill in one more thing (PLAN.md §4.1). */
export function ProfileScreen() {
  const { t } = useTranslation()
  const me = useMe()
  const { setUser } = useAuth()
  const publicView = useApi<PublicProfile>(`/users/${me.id}`)
  const fileRef = useRef<HTMLInputElement>(null)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const upload = async (file: File) => {
    setUploading(true)
    setError(null)
    try {
      const form = new FormData()
      form.append('file', file)
      setUser(await api.post<Me>('/me/photo', form))
    } catch (err) {
      setError(errorCode(err))
    } finally {
      setUploading(false)
    }
  }

  const next = me.strength.missing[0]

  return (
    <div className="screen">
      <ScreenHeader
        large
        title={t('profile.title')}
        action={
          <Link to="/settings" className="icon-btn" aria-label={t('settings.title')}>
            <Icon name="settings" />
          </Link>
        }
      />
      <ProfileView
        name={me.displayName}
        age={me.age}
        countryCode={me.countryCode}
        photoUrl={me.photoUrl}
        bio={me.bio}
        languages={me.languages}
        interests={me.interests}
        badges={me.badges}
        rating={publicView.data?.rating}
        fullName={me.fullName}
        avatarAction={
          <>
            <input
              ref={fileRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              aria-label={t('profile.photoInput')}
              onChange={(e) => {
                const file = e.target.files?.[0]
                if (file) void upload(file)
                e.target.value = ''
              }}
            />
            <Button size="sm" variant="secondary" loading={uploading} onClick={() => fileRef.current?.click()}>
              {me.photoUrl ? t('profile.changePhoto') : t('profile.addPhoto')}
            </Button>
          </>
        }
      >
        <section className="screen__section">
          <FormError code={error} />
          {me.strength.percent < 100 && next && (
            <Card variant="filled" as="div">
              <CardBody>
                <div className="profile__strength">
                  <span>{t('profile.strength', { percent: me.strength.percent })}</span>
                  <progress max={100} value={me.strength.percent} aria-label={t('profile.strengthLabel')} />
                </div>
                <p className="screen__meta">{t(`profile.next_${next}`)}</p>
                {next === 'contactVerified' ? (
                  <ConfirmEmail email={me.email} />
                ) : next === 'photo' ? (
                  <Button size="sm" variant="brand" loading={uploading} onClick={() => fileRef.current?.click()}>
                    {t('profile.addPhoto')}
                  </Button>
                ) : (
                  <Link to="/profile/edit" className="btn btn--brand btn--sm">
                    {t('profile.addNow')}
                  </Link>
                )}
              </CardBody>
            </Card>
          )}
          <Link to="/profile/edit" className="btn btn--secondary btn--md btn--block">
            {t('profile.edit')}
          </Link>
        </section>
      </ProfileView>
      <div className="screen__section" />
    </div>
  )
}

/** Confirms the email with an emailed code; earns the contact-verified badge. */
function ConfirmEmail({ email }: { email: string }) {
  const { t } = useTranslation()
  const { setUser } = useAuth()
  const [sent, setSent] = useState(false)
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

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

  if (!sent) {
    return (
      <Button size="sm" variant="brand" loading={busy} onClick={() => void run(async () => {
        await api.post('/auth/email/confirm/start')
        setSent(true)
      })}>
        {t('profile.confirmEmail')}
      </Button>
    )
  }

  return (
    <form
      className="screen__stack"
      onSubmit={(e) => {
        e.preventDefault()
        void run(async () => setUser(await api.post<Me>('/auth/email/confirm', { code })))
      }}
    >
      <TextField
        label={t('auth.code')}
        inputMode="numeric"
        autoComplete="one-time-code"
        maxLength={6}
        value={code}
        onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
        hint={t('auth.codeSent', { email })}
      />
      <FormError code={error} />
      <Button type="submit" size="sm" variant="brand" loading={busy}>
        {t('auth.verifyCode')}
      </Button>
    </form>
  )
}

