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
  /** The viewer's latest join request; only on the preview, for a signed-in viewer. */
  myRequest: MyCardRequest | null
  /** Open join requests; only for the owner and co-admins. */
  pendingRequestCount: number | null
}

export type RequestStatus = 'requested' | 'approved' | 'rejected' | 'withdrawn' | 'expired'

export type MyCardRequest = { id: string; status: RequestStatus; createdAt: string }

/** A join request as the owner and co-admins see it. */
export type CardRequest = { id: string; person: Person; message: string | null; status: RequestStatus; createdAt: string }

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
  /** Open join requests; 0 for plain members. */
  pendingRequests: number
}

export type PlanCategory = 'hike' | 'dayTrip' | 'food' | 'nightlife' | 'tour' | 'beach' | 'transport' | 'other'
export type PlanAudience = 'open' | 'groupsOnly'
export type PlanStatus = 'open' | 'full' | 'cancelled' | 'done'
export type LocationPrecision = 'exact' | 'regional'
export type PlanAccess = 'public' | 'participant' | 'host'
export type LatLng = { lat: number; lng: number }

/** A place picked as a meeting point; `area` is the coarse public label. */
export type Place = { name: string; area: string; lat: number; lng: number }

/**
 * An Activity Plan as the viewer may see it. `meetingPoint` and an exact `destination` arrive only for participants;
 * `participants` only for participants and members of the plan's card.
 */
export type Plan = {
  id: string
  cardId: string
  cardName: string | null
  title: string
  category: PlanCategory
  startsAt: string
  timeZoneId: string
  localDate: string
  localTime: string
  areaLabel: string
  distance: { km: number; underOneKm: boolean } | null
  meetingPoint: { name: string; lat: number; lng: number } | null
  destination: string | null
  destinationPrecision: LocationPrecision
  purpose: string | null
  seatLimit: number
  seatsTaken: number
  audience: PlanAudience
  status: PlanStatus
  access: PlanAccess
  canManage: boolean
  canSelfJoin: boolean
  host: Person
  participants: Person[] | null
}

/** A plan in a list. */
export type PlanSummary = Pick<
  Plan,
  'id' | 'title' | 'category' | 'startsAt' | 'timeZoneId' | 'localDate' | 'localTime' | 'areaLabel' | 'seatLimit' | 'seatsTaken' | 'audience' | 'status' | 'host'
> & { joined: boolean }
