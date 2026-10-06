import { useEffect, useState } from 'react'
import { api } from './api'
import { countryName } from './countries'
import type { MyCard, Place, PlanCategory } from './types'

export type DiscoverQuery = {
  origin: Place
  from: string
  to: string
  radiusKm?: number
  category?: PlanCategory
  maxSeats?: number
  sort?: 'distance' | 'date'
}

export function discoverPath(q: DiscoverQuery): string {
  const params = new URLSearchParams({
    lat: String(q.origin.lat),
    lng: String(q.origin.lng),
    from: q.from,
    to: q.to,
  })
  if (q.radiusKm) params.set('radiusKm', String(q.radiusKm))
  if (q.category) params.set('category', q.category)
  if (q.maxSeats) params.set('maxSeats', String(q.maxSeats))
  if (q.sort) params.set('sort', q.sort)
  return `/discover?${params.toString()}`
}

/** The plan page link carries the search origin so it can show the same rounded distance. */
export function planLink(id: string, origin?: Place | null): string {
  return origin ? `/plans/${id}?lat=${origin.lat}&lng=${origin.lng}` : `/plans/${id}`
}

const storeKey = (card: MyCard) => `travether.origin.${card.id}.${card.regions[0] ?? ''}`

function readStored(card: MyCard): Place | null {
  try {
    const raw = localStorage.getItem(storeKey(card))
    return raw ? (JSON.parse(raw) as Place) : null
  } catch {
    return null
  }
}

/** Remembers a place the traveler picked for this trip, on this device. */
export function rememberOrigin(card: MyCard, place: Place) {
  try {
    localStorage.setItem(storeKey(card), JSON.stringify(place))
  } catch {
    // Private mode or storage blocked: the trip's first city is used next time.
  }
}

/**
 * Where to search from for a trip: the place picked earlier on this device, else the trip's first city,
 * geocoded through /api/places.
 */
export function useTripOrigin(card: MyCard | null, lang: string): Place | null {
  const [resolved, setResolved] = useState<{
    key: string
    place: Place
  } | null>(null)
  const key = card ? storeKey(card) : ''
  const stored = card ? readStored(card) : null
  const hasStored = stored !== null

  useEffect(() => {
    if (!card || hasStored || !card.regions[0] || resolved?.key === key) return
    const abort = new AbortController()
    const q = `${card.regions[0]}, ${countryName(card.countryCode, lang)}`
    api
      .get<Place[]>(`/places/search?q=${encodeURIComponent(q)}`, abort.signal)
      .then((found) => {
        if (found[0]) setResolved({ key, place: found[0] })
      })
      .catch(() => {
        // No suggestion: the screen asks the traveler to pick a place.
      })
    return () => abort.abort()
  }, [card, hasStored, key, lang, resolved?.key])

  return stored ?? (resolved?.key === key ? resolved.place : null)
}
