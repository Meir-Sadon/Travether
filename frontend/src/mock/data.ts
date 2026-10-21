/*
 * Dummy data for the Phase 0 clickable mockups. Nothing here comes from the API.
 * Names, places and dates follow the prototype (design/prototype).
 */

export type CategoryKey = 'hike' | 'dayTrip' | 'food' | 'nightlife' | 'tour' | 'beach' | 'transport' | 'other'

export const categoryTint: Record<CategoryKey, string> = {
  hike: 'var(--tint-hike)',
  dayTrip: 'var(--tint-day-trip)',
  food: 'var(--tint-food)',
  nightlife: 'var(--tint-nightlife)',
  tour: 'var(--tint-tour)',
  beach: 'var(--tint-beach)',
  transport: 'var(--tint-transport)',
  other: 'var(--tint-other)',
}

export type MockPerson = {
  id: string
  name: string
  age: number
  country: string
  flag: string
  tint: string
  /** null until the person has 3+ reviews (PLAN.md §4.6). */
  rating: number | null
  reviewCount: number
  badges: ('contact' | 'photo' | 'id')[]
  languages: string[]
}

export const people: Record<string, MockPerson> = {
  noa: { id: 'noa', name: 'Noa', age: 27, country: 'Israel', flag: '🇮🇱', tint: '#FBE1DE', rating: 4.8, reviewCount: 5, badges: ['contact', 'photo'], languages: ['Hebrew', 'English'] },
  itay: { id: 'itay', name: 'Itay', age: 26, country: 'Israel', flag: '🇮🇱', tint: '#DDEBF7', rating: 4.8, reviewCount: 4, badges: ['contact'], languages: ['Hebrew', 'English'] },
  maya: { id: 'maya', name: 'Maya', age: 27, country: 'Israel', flag: '🇮🇱', tint: '#E3EFE6', rating: null, reviewCount: 1, badges: ['contact'], languages: ['Hebrew', 'English'] },
  lena: { id: 'lena', name: 'Lena', age: 26, country: 'Germany', flag: '🇩🇪', tint: '#F3E3CF', rating: 4.9, reviewCount: 12, badges: ['contact', 'photo', 'id'], languages: ['German', 'English'] },
  jonas: { id: 'jonas', name: 'Jonas', age: 27, country: 'Germany', flag: '🇩🇪', tint: '#DDEBF7', rating: 4.8, reviewCount: 9, badges: ['contact', 'photo'], languages: ['German', 'English'] },
  sofia: { id: 'sofia', name: 'Sofía', age: 29, country: 'Spain', flag: '🇪🇸', tint: '#FBE1DE', rating: 4.7, reviewCount: 5, badges: ['contact'], languages: ['Spanish', 'English'] },
  roni: { id: 'roni', name: 'Roni', age: 22, country: 'Israel', flag: '🇮🇱', tint: '#E3EFE6', rating: null, reviewCount: 0, badges: ['contact'], languages: ['Hebrew', 'English'] },
  omer: { id: 'omer', name: 'Omer', age: 23, country: 'Israel', flag: '🇮🇱', tint: '#FBE7D9', rating: null, reviewCount: 0, badges: [], languages: ['Hebrew'] },
  tom: { id: 'tom', name: 'Tom', age: 25, country: 'UK', flag: '🇬🇧', tint: '#E8E1F5', rating: 4.6, reviewCount: 8, badges: ['contact'], languages: ['English'] },
  chris: { id: 'chris', name: 'Chris', age: 28, country: 'UK', flag: '🇬🇧', tint: '#F3E3CF', rating: null, reviewCount: 2, badges: ['contact'], languages: ['English'] },
  dan: { id: 'dan', name: 'Dan', age: 30, country: 'USA', flag: '🇺🇸', tint: '#E8E1F5', rating: null, reviewCount: 0, badges: [], languages: ['English'] },
}

export const me = people.noa

export type MockPlan = {
  id: string
  tripId: string
  category: CategoryKey
  title: string
  when: string
  /** Public, rounded distance from the viewer. */
  distance: string
  area: string
  /** Shown only after approval (location privacy). */
  exactMeetingPoint: string
  hostGroup: string
  hostIds: string[]
  goingIds: string[]
  seatLimit: number
  audience: 'open' | 'groupsOnly'
  purpose: string
  languages: string
  /** Position on the mock map, in percent of the map box. */
  map: { x: number; y: number }
  bookable?: string
}

export const plans: MockPlan[] = [
  {
    id: 'sanctuary',
    tripId: 'berlin-bp',
    category: 'dayTrip',
    title: 'Elephant sanctuary + waterfall',
    when: 'Thu 16 Oct · 08:00 – 16:00',
    distance: '~1 km',
    area: 'Nimman area',
    exactMeetingPoint: '7-Eleven, Huay Kaew Rd · 07:50',
    hostGroup: 'Berlin Backpackers',
    hostIds: ['lena', 'jonas'],
    goingIds: ['lena', 'jonas', 'tom', 'chris'],
    seatLimit: 8,
    audience: 'open',
    purpose: 'Ethical sanctuary (no riding), then the sticky waterfall. Splitting a songthaew, about 200 THB each.',
    languages: 'EN · DE',
    map: { x: 38, y: 34 },
    bookable: 'Sanctuary day ticket',
  },
  {
    id: 'khao-soi',
    tripId: 'sofia-solo',
    category: 'food',
    title: 'Khao soi tasting tour',
    when: 'Fri 17 Oct · 12:30',
    distance: '~2 km',
    area: 'Old City',
    exactMeetingPoint: 'Khao Soi Mae Manee, Ratchaphakhinai Rd',
    hostGroup: 'Sofía (solo)',
    hostIds: ['sofia'],
    goingIds: ['sofia'],
    seatLimit: 6,
    audience: 'open',
    purpose: 'Three of the best khao soi spots in one afternoon. Come hungry.',
    languages: 'EN · ES',
    map: { x: 58, y: 52 },
  },
  {
    id: 'mae-sa',
    tripId: 'tlv-asia',
    category: 'hike',
    title: 'Mae Sa waterfall trail',
    when: 'Sat 18 Oct · 07:00',
    distance: '~3 km',
    area: 'Santitham',
    exactMeetingPoint: 'Santitham market, north gate',
    hostGroup: 'Tel Aviv → Asia',
    hostIds: ['roni', 'omer'],
    goingIds: ['roni', 'omer', 'maya'],
    seatLimit: 5,
    audience: 'open',
    purpose: 'Ten tiers of waterfalls, easy-moderate. Renting scooters there.',
    languages: 'HE · EN',
    map: { x: 26, y: 62 },
  },
  {
    id: 'live-music',
    tripId: 'uk-lads',
    category: 'nightlife',
    title: 'Live music at Nimman',
    when: 'Sat 18 Oct · 21:00',
    distance: '~5 km',
    area: 'Nimmanhaemin',
    exactMeetingPoint: 'Warmup Café, Nimman Soi 15',
    hostGroup: 'UK lads',
    hostIds: ['tom', 'chris'],
    goingIds: ['tom', 'chris'],
    seatLimit: 8,
    audience: 'groupsOnly',
    purpose: 'Bands from 9, then wherever the night goes.',
    languages: 'EN',
    map: { x: 70, y: 24 },
  },
  {
    id: 'sunrise-hike',
    tripId: 'cm-crew',
    category: 'hike',
    title: 'Sunrise hike to Doi Suthep',
    when: 'Tue 14 Oct · 05:30',
    distance: '~2 km',
    area: 'Old City',
    exactMeetingPoint: 'Monk’s Trail trailhead, Suthep Rd',
    hostGroup: 'Chiang Mai Crew',
    hostIds: ['noa'],
    goingIds: ['noa', 'itay', 'maya'],
    seatLimit: 8,
    audience: 'open',
    purpose: 'Monk’s trail up, sunrise at the temple, breakfast after. Moderate pace.',
    languages: 'HE · EN',
    map: { x: 46, y: 44 },
  },
  {
    id: 'night-market',
    tripId: 'cm-crew',
    category: 'food',
    title: 'Night market food crawl',
    when: 'Wed 15 Oct · 19:00',
    distance: '~1 km',
    area: 'Chang Klan',
    exactMeetingPoint: 'Night Bazaar main entrance',
    hostGroup: 'Chiang Mai Crew',
    hostIds: ['itay'],
    goingIds: ['itay', 'noa', 'maya'],
    seatLimit: 10,
    audience: 'groupsOnly',
    purpose: 'Grazing through the night bazaar. Bring cash.',
    languages: 'HE · EN',
    map: { x: 62, y: 40 },
  },
]

export type MockTrip = {
  id: string
  name: string
  countryCode: string
  country: string
  regions: string[]
  dates: string
  visibility: 'public' | 'inviteOnly'
  description: string
  tint: string
  shareSlug: string
  members: { id: string; role: 'owner' | 'coAdmin' | 'member' }[]
  pendingRequests: number
}

export const trips: MockTrip[] = [
  {
    id: 'cm-crew',
    name: 'Chiang Mai Crew',
    countryCode: 'TH',
    country: 'Thailand',
    regions: ['Chiang Mai', 'Pai'],
    dates: '12 – 26 Oct',
    visibility: 'public',
    description: 'Three friends after the army, into hikes, street food and the odd night out. Happy to share rides.',
    tint: '#CFE3E6',
    shareSlug: 'cm-crew-7K2',
    members: [
      { id: 'noa', role: 'owner' },
      { id: 'itay', role: 'coAdmin' },
      { id: 'maya', role: 'member' },
    ],
    pendingRequests: 1,
  },
]

export const myTripIds = ['cm-crew']

export type MockRequest = {
  id: string
  personId: string
  party: number
  target: string
  message: string
}

export const incomingRequests: MockRequest[] = [
  { id: 'r-lena', personId: 'lena', party: 1, target: 'Chiang Mai Crew', message: 'Solo in Chiang Mai the same dates. Love hiking!' },
  { id: 'r-dan', personId: 'dan', party: 2, target: 'Sunrise hike to Doi Suthep', message: 'Me and my brother, early birds.' },
]

export type MockChat = {
  id: string
  kind: 'trip' | 'plan'
  category?: CategoryKey
  title: string
  time: string
  last: string
  memberIds: string[]
  meetingPoint?: string
  unread: number
}

export const chats: MockChat[] = [
  { id: 'cm-crew', kind: 'trip', title: 'Chiang Mai Crew', time: '10:24', last: 'Itay: booked the scooter for Pai', memberIds: ['noa', 'itay', 'maya'], unread: 2 },
  {
    id: 'sanctuary',
    kind: 'plan',
    category: 'dayTrip',
    title: 'Elephant sanctuary',
    time: '09:02',
    last: 'Lena: see you at 8 by the 7-Eleven!',
    memberIds: ['lena', 'jonas', 'noa', 'itay', 'maya', 'tom', 'chris'],
    meetingPoint: '7-Eleven, Huay Kaew Rd · 07:50',
    unread: 1,
  },
  { id: 'sunrise-hike', kind: 'plan', category: 'hike', title: 'Sunrise hike to Doi Suthep', time: 'Mon', last: 'You: bring headlamps', memberIds: ['noa', 'itay', 'maya'], unread: 0 },
]

export type MockMessage = { from: string; text: string; time: string }

export const messages: Record<string, MockMessage[]> = {
  sanctuary: [
    { from: 'lena', text: 'Hi all! Songthaew is booked for 7:50 🙌', time: '08:41' },
    { from: 'jonas', text: 'Bring swimwear for the waterfall', time: '08:43' },
    { from: 'noa', text: 'Perfect, we’re 3. See you there!', time: '08:55' },
    { from: 'lena', text: 'See you at 8 by the 7-Eleven!', time: '09:02' },
  ],
  'cm-crew': [
    { from: 'maya', text: 'Who’s up for the sunrise hike?', time: '10:02' },
    { from: 'itay', text: 'Booked the scooter for Pai', time: '10:24' },
  ],
  'sunrise-hike': [{ from: 'noa', text: 'Bring headlamps', time: 'Mon' }],
}

export const reviewsAboutMe = [
  { from: 'Tom', country: 'UK', date: 'Sep 2026', stars: 5, text: 'Organised the whole hike and checked everyone got back safe.' },
  { from: 'Ana', country: 'Argentina', date: 'Sep 2026', stars: 4, text: 'Great company for the cooking class, a bit late though!' },
]

export function person(id: string): MockPerson {
  const p = people[id]
  if (!p) throw new Error(`Unknown mock person ${id}`)
  return p
}

export function plan(id: string | undefined): MockPlan | undefined {
  return plans.find((p) => p.id === id)
}

export function trip(id: string | undefined): MockTrip | undefined {
  return trips.find((t) => t.id === id)
}

export function seatsLeft(p: MockPlan) {
  return p.seatLimit - p.goingIds.length
}
