import { useMemo, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { FormError, Segmented, SelectField, TextField } from '../components'
import { countryOptions } from '../lib/countries'
import { todayIso } from '../lib/dates'
import type { Card, CardVisibility } from '../lib/types'

export type CardFields = {
  name: string
  countryCode: string
  regions: string[]
  startsOn: string
  endsOn: string
  description: string
  visibility: CardVisibility
}

type CardFormProps = {
  id: string
  initial?: Card
  error: string | null
  onSubmit: (fields: CardFields) => void
}

/**
 * Name, country, cities, dates, visibility, about. Shared by "New Vacation Card" and "Edit".
 * Submitted by a button elsewhere (a sheet footer) through the form's `id`.
 */
export function CardForm({ id, initial, error, onSubmit }: CardFormProps) {
  const { t, i18n } = useTranslation()
  const countries = useMemo(() => countryOptions(i18n.language), [i18n.language])
  const [name, setName] = useState(initial?.name ?? '')
  const [country, setCountry] = useState(initial?.countryCode ?? '')
  const [regions, setRegions] = useState(initial?.regions.join(', ') ?? '')
  const [startsOn, setStartsOn] = useState(initial?.startsOn ?? '')
  const [endsOn, setEndsOn] = useState(initial?.endsOn ?? '')
  const [description, setDescription] = useState(initial?.description ?? '')
  const [visibility, setVisibility] = useState<CardVisibility>(initial?.visibility ?? 'public')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    onSubmit({
      name: name.trim(),
      countryCode: country,
      regions: regions.split(',').map((r) => r.trim()).filter(Boolean),
      startsOn,
      endsOn,
      description,
      visibility,
    })
  }

  return (
    <form id={id} className="screen__stack" onSubmit={submit} noValidate>
      <TextField label={t('create.tripName')} required maxLength={80} placeholder="Chiang Mai Crew" value={name} onChange={(e) => setName(e.target.value)} />
      <SelectField
        label={t('create.country')}
        required
        placeholder={t('signup.chooseCountry')}
        options={countries.map((c) => ({ value: c.code, label: c.name }))}
        value={country}
        onChange={(e) => setCountry(e.target.value)}
      />
      <TextField label={t('create.cities')} required placeholder="Chiang Mai, Pai" hint={t('create.citiesHint')} value={regions} onChange={(e) => setRegions(e.target.value)} />
      <div className="create__two">
        <TextField label={t('create.from')} type="date" required min={initial ? undefined : todayIso()} value={startsOn} onChange={(e) => setStartsOn(e.target.value)} />
        <TextField label={t('create.to')} type="date" required min={startsOn || todayIso()} value={endsOn} onChange={(e) => setEndsOn(e.target.value)} />
      </div>
      <Segmented
        label={t('create.visibility')}
        value={visibility}
        onChange={setVisibility}
        options={[
          { value: 'public', label: t('trip.public') },
          { value: 'inviteOnly', label: t('trip.inviteOnly') },
        ]}
      />
      <p className="screen__meta">{t(`create.visibility_${visibility}`)}</p>
      <TextField multiline label={t('create.about')} maxLength={1000} placeholder={t('create.aboutPlaceholder')} value={description} onChange={(e) => setDescription(e.target.value)} />
      <FormError code={error} />
    </form>
  )
}
