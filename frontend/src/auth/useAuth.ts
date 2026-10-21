import { createContext, useContext } from 'react'
import type { Me } from './types'

export type AuthState = {
  /** undefined while the session is loading, null for visitors. */
  user: Me | null | undefined
  setUser: (user: Me | null) => void
  refresh: () => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}

/** The signed-in user, for screens behind <RequireAuth>. */
export function useMe(): Me {
  const { user } = useAuth()
  if (!user) throw new Error('useMe must be used behind <RequireAuth>')
  return user
}

/** Only same-site paths, so a crafted `?next=` can't send people elsewhere. */
export function safeNext(next: string | null): string {
  return next && next.startsWith('/') && !next.startsWith('//') ? next : '/'
}
