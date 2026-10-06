import type { Badge } from '../auth/types'

export type RatingSummary = { average: number | null; count: number }

/** Someone else's profile, already cut to what the viewer may see by the API. */
export type PublicProfile = {
  id: string
  displayName: string
  age: number
  countryCode: string
  photoUrl: string | null
  bio: string | null
  languages: string[]
  interests: string[]
  badges: Badge[]
  rating: RatingSummary
  memberSince: string
  /** Only for people who share a trip or plan with the viewer. */
  fullName: string | null
  /** Only for moderators. */
  email: string | null
}

/** A person in a list (members, participants, requesters): public fields only. */
export type Person = {
  id: string
  displayName: string
  age: number
  countryCode: string
  photoUrl: string | null
  badges: Badge[]
}
