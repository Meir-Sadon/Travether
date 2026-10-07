import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import type { Me } from './types'
import { AuthContext } from './useAuth'

type Session = { user: Me | null; needsConsent?: boolean }

const loadSession = () => api.get<Session>('/auth/me').catch((): Session => ({ user: null }))

/** Loads the session once (`GET /api/auth/me`); the cookie itself is httpOnly and never read here. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null | undefined>(undefined)
  const [needsConsent, setNeedsConsent] = useState(false)

  const apply = useCallback((s: Session) => {
    setUser(s.user)
    setNeedsConsent(!!s.user && !!s.needsConsent)
  }, [])

  const refresh = useCallback(async () => apply(await loadSession()), [apply])

  const acceptConsent = useCallback(async () => {
    await api.post('/me/consents/legal')
    setNeedsConsent(false)
  }, [])

  const logout = useCallback(async () => {
    await api.post('/auth/logout')
    setUser(null)
  }, [])

  useEffect(() => {
    let live = true
    void loadSession().then((s) => live && apply(s))
    return () => {
      live = false
    }
  }, [apply])

  const value = useMemo(
    () => ({ user, setUser, refresh, logout, needsConsent, acceptConsent }),
    [user, refresh, logout, needsConsent, acceptConsent],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
