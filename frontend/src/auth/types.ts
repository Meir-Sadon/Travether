export type Badge = 'contactVerified' | 'photoVerified' | 'idVerified'

/** The signed-in user's own profile (`MeDto`). */
export type Me = {
  id: string
  displayName: string
  fullName: string
  email: string
  phone: string | null
  dateOfBirth: string
  age: number
  countryCode: string
  photoUrl: string | null
  bio: string | null
  languages: string[]
  interests: string[]
  badges: Badge[]
  role: 'traveler' | 'moderator'
  hasPassword: boolean
  createdAt: string
  strength: { percent: number; missing: ProfileItem[] }
}

/** Profile parts the strength meter asks for, most valuable first. */
export type ProfileItem = 'contactVerified' | 'photo' | 'bio' | 'interests' | 'languages'

export type AuthResult =
  | { status: 'signedIn'; user: Me }
  | { status: 'needsProfile'; signupToken: string; email: string; suggestedName: string | null }

export type Providers = { googleClientId: string | null; appleClientId: string | null; appleRedirectUri: string | null }
