import type { Card, MyCard, Person } from '../lib/types'
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
