import type { PlanCategory } from './types'
import { parseDate } from './dates'

export const planCategories: PlanCategory[] = ['hike', 'dayTrip', 'food', 'nightlife', 'tour', 'beach', 'transport', 'other']

export const categoryTint: Record<PlanCategory, string> = {
  hike: 'var(--tint-hike)',
  dayTrip: 'var(--tint-day-trip)',
  food: 'var(--tint-food)',
  nightlife: 'var(--tint-nightlife)',
  tour: 'var(--tint-tour)',
  beach: 'var(--tint-beach)',
  transport: 'var(--tint-transport)',
  other: 'var(--tint-other)',
}

/** "Wed 14 Oct · 05:30" in the meeting point's local time, as the host entered it. */
export function formatPlanWhen(localDate: string, localTime: string, lang: string): string {
  const day = new Intl.DateTimeFormat(lang, { weekday: 'short', day: 'numeric', month: 'short' }).format(parseDate(localDate))
  return `${day} · ${localTime.slice(0, 5)}`
}

/** A link that opens the exact meeting point in the phone's map app (or the browser). */
export function mapLink(lat: number, lng: number): string {
  return `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=17/${lat}/${lng}`
}
