import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, FormError, Icon, TextField } from '../components'
import { api, errorCode } from '../lib/api'
import type { Place } from '../lib/types'
import './PlaceField.css'

type PlaceFieldProps = {
  label: string
  hint?: string
  value: Place | null
  onChange: (place: Place | null) => void
}

/** Search for a meeting point (or use the phone's location). Suggestions come from /api/places. */
export function PlaceField({ label, hint, value, onChange }: PlaceFieldProps) {
  const { t } = useTranslation()
  const [query, setQuery] = useState(value?.name ?? '')
  const [results, setResults] = useState<Place[]>([])
  const [error, setError] = useState<string | null>(null)
  const [locating, setLocating] = useState(false)

  useEffect(() => {
    const q = query.trim()
    if (q.length < 2 || q === value?.name) return
    const abort = new AbortController()
    const timer = setTimeout(() => {
      api
        .get<Place[]>(`/places/search?q=${encodeURIComponent(q)}`, abort.signal)
        .then((found) => {
          setResults(found)
          setError(null)
        })
        .catch((err: unknown) => {
          if (!abort.signal.aborted) setError(errorCode(err))
        })
    }, 300)
    return () => {
      clearTimeout(timer)
      abort.abort()
    }
  }, [query, value?.name])

  const pick = (place: Place) => {
    onChange(place)
    setQuery(place.name)
    setResults([])
  }

  const locate = () => {
    if (!('geolocation' in navigator)) return setError('LocationUnavailable')
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        api
          .get<Place>(`/places/reverse?lat=${pos.coords.latitude}&lng=${pos.coords.longitude}`)
          .then(pick)
          .catch((err: unknown) => setError(errorCode(err)))
          .finally(() => setLocating(false))
      },
      () => {
        setLocating(false)
        setError('LocationUnavailable')
      },
      { enableHighAccuracy: true, timeout: 10_000 },
    )
  }

  return (
    <div className="place-field">
      <TextField
        label={label}
        hint={value ? t('create.placePicked', { area: value.area }) : hint}
        value={query}
        autoComplete="off"
        onChange={(e) => {
          setQuery(e.target.value)
          if (value) onChange(null)
          if (e.target.value.trim().length < 2) setResults([])
        }}
      />
      {results.length > 0 && (
        <ul className="list-reset place-field__results" aria-label={t('create.placeSuggestions')}>
          {results.map((p) => (
            <li key={`${p.lat},${p.lng},${p.name}`}>
              <button type="button" className="place-field__option" onClick={() => pick(p)}>
                <Icon name="pin" size={16} />
                <span>
                  <strong>{p.name}</strong>
                  <span className="screen__meta">{p.area}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <Button size="sm" variant="ghost" icon="pin" loading={locating} onClick={locate}>
        {t('create.useMyLocation')}
      </Button>
      <FormError code={error} />
    </div>
  )
}
