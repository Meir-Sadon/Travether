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

export type CardVisibility = 'public' | 'inviteOnly'
export type CardRole = 'owner' | 'coAdmin' | 'member'
export type CardAccess = 'preview' | 'member' | 'coAdmin' | 'owner'

export type CardMember = { person: Person; role: CardRole; joinedAt: string }

/** A Vacation Card as the viewer may see it; members and shareSlug only arrive for members. */
export type Card = {
  id: string
  name: string
  countryCode: string
  regions: string[]
  startsOn: string
  endsOn: string
  description: string | null
  coverUrl: string | null
  visibility: CardVisibility
  memberCount: number
  access: CardAccess
  shareSlug: string | null
  members: CardMember[] | null
}

/** A card on Home. */
export type MyCard = {
  id: string
  name: string
  countryCode: string
  regions: string[]
  startsOn: string
  endsOn: string
  coverUrl: string | null
  visibility: CardVisibility
  role: CardRole
  memberCount: number
  planCount: number
  membersPreview: Person[]
}
