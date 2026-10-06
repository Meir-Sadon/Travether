import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { Me } from '../auth/types'
import { useAuth, useMe } from '../auth/useAuth'
import { Button, Chip, FormError, SelectField, TextField } from '../components'
import { ScreenHeader } from '../layout/ScreenHeader'
import { api, errorCode } from '../lib/api'
import { countryOptions, interestTags, languageCodes, languageName } from '../lib/countries'

/** Edit the optional profile fields; every one of them can be filled in later. */
export function EditProfileScreen() {
  const { t, i18n } = useTranslation()
  const me = useMe()
  const { setUser } = useAuth()
  const navigate = useNavigate()
  const [displayName, setDisplayName] = useState(me.displayName)
  const [country, setCountry] = useState(me.countryCode)
  const [bio, setBio] = useState(me.bio ?? '')
  const [phone, setPhone] = useState(me.phone ?? '')
  const [interests, setInterests] = useState(me.interests)
  const [languages, setLanguages] = useState(me.languages)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const countries = useMemo(() => countryOptions(i18n.language), [i18n.language])
  // Keep any language the user already has, even if it isn't one of the offered chips.
  const languageChoices = useMemo(() => [...new Set([...languageCodes, ...me.languages])], [me.languages])

  const toggle = (list: string[], set: (v: string[]) => void, value: string, on: boolean) =>
    set(on ? [...list, value] : list.filter((x) => x !== value))

  const save = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const updated = await api.patch<Me>('/me', {
        displayName,
        countryCode: country,
        bio,
        phone: phone.replace(/[\s()-]/g, ''),
        interests,
        languages,
      })
      setUser(updated)
      void navigate('/profile')
    } catch (err) {
      setError(errorCode(err))
      setBusy(false)
    }
  }

  return (
    <form className="screen" style={{ minBlockSize: '100dvh' }} onSubmit={(e) => void save(e)} noValidate>
      <ScreenHeader back backTo="/profile" title={t('profile.edit')} />
      <section className="screen__section">
        <TextField label={t('signup.firstName')} required maxLength={40} value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        <SelectField
          label={t('signup.country')}
          options={countries.map((c) => ({ value: c.code, label: c.name }))}
          value={country}
          onChange={(e) => setCountry(e.target.value)}
        />
        <TextField label={t('profile.bioLabel')} multiline maxLength={500} rows={4} value={bio} onChange={(e) => setBio(e.target.value)} hint={t('profile.bioHint')} />
        <TextField
          label={t('profile.phone')}
          type="tel"
          autoComplete="tel"
          placeholder="+972 50 123 4567"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          hint={t('profile.phoneHint')}
        />
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
          {languageChoices.map((l) => (
            <Chip key={l} size="md" selected={languages.includes(l)} onToggle={(on) => toggle(languages, setLanguages, l, on)}>
              {languageName(l, i18n.language)}
            </Chip>
          ))}
        </div>
        <FormError code={error} />
      </section>
      <footer className="screen__footer">
        <Button type="submit" size="lg" block loading={busy} disabled={!displayName.trim()}>
          {t('profile.save')}
        </Button>
      </footer>
    </form>
  )
}
