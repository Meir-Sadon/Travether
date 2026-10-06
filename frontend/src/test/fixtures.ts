import type { Card, MyCard, Person, Plan, PlanSummary } from '../lib/types'
import { testUser } from './mockApi'

export const noa: Person = { id: testUser.id, displayName: 'Noa', age: 30, countryCode: 'IL', photoUrl: null, badges: ['contactVerified'] }
export const lena: Person = { id: 'u-lena', displayName: 'Lena', age: 26, countryCode: 'DE', photoUrl: null, badges: [] }

export const crewCard: Card = {
  id: 'card-1',
  name: 'Chiang Mai Crew',
  countryCode: 'TH',
  regions: ['Chiang Mai', 'Pai'],
  startsOn: '2026-10-12',
  endsOn: '2026-10-26',
  description: 'Hikes and khao soi.',
  coverUrl: null,
  visibility: 'public',
  memberCount: 2,
  access: 'owner',
  shareSlug: 'abcDEF2345',
  members: [
    { person: noa, role: 'owner', joinedAt: '2026-10-01T00:00:00Z' },
    { person: lena, role: 'member', joinedAt: '2026-10-02T00:00:00Z' },
  ],
  myRequest: null,
  pendingRequestCount: 0,
}

export const crewTile: MyCard = {
  id: crewCard.id,
  name: crewCard.name,
  countryCode: 'TH',
  regions: crewCard.regions,
  startsOn: crewCard.startsOn,
  endsOn: crewCard.endsOn,
  coverUrl: null,
  visibility: 'public',
  role: 'owner',
  memberCount: 2,
  planCount: 0,
  membersPreview: [noa, lena],
  pendingRequests: 0,
}

export const previewOf = (card: Card): Card => ({ ...card, access: 'preview', shareSlug: null, members: null, pendingRequestCount: null })

export const hikePlan: Plan = {
  id: 'plan-1',
  cardId: crewCard.id,
  cardName: crewCard.name,
  title: 'Sunrise hike to Doi Suthep',
  category: 'hike',
  startsAt: '2026-10-13T22:30:00Z',
  timeZoneId: 'Asia/Bangkok',
  localDate: '2026-10-14',
  localTime: '05:30:00',
  areaLabel: 'Old City, Chiang Mai',
  distance: null,
  meetingPoint: { name: 'Tha Phae Gate', lat: 18.7877, lng: 98.9933 },
  destination: 'Wat Phra That Doi Suthep',
  destinationPrecision: 'exact',
  purpose: "Monk's trail up, breakfast after.",
  seatLimit: 4,
  seatsTaken: 2,
  audience: 'open',
  status: 'open',
  access: 'host',
  canManage: true,
  canSelfJoin: false,
  host: noa,
  participants: [noa, lena],
}

/** What someone outside the plan's card sees. */
export const publicPlan: Plan = {
  ...hikePlan,
  cardName: null,
  access: 'public',
  canManage: false,
  meetingPoint: null,
  destination: null,
  distance: { km: 2, underOneKm: false },
  host: lena,
  participants: null,
}

export const hikeSummary: PlanSummary = {
  id: hikePlan.id,
  title: hikePlan.title,
  category: 'hike',
  startsAt: hikePlan.startsAt,
  timeZoneId: hikePlan.timeZoneId,
  localDate: hikePlan.localDate,
  localTime: hikePlan.localTime,
  areaLabel: hikePlan.areaLabel,
  seatLimit: 4,
  seatsTaken: 2,
  audience: 'open',
  status: 'open',
  host: noa,
  joined: true,
}
