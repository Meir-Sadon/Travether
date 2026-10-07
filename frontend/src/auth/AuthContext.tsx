import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import { syncAnalytics } from '../lib/telemetry'
import type { Me } from './types'
import { AuthContext } from './useAuth'

type Session = { user: Me | null; needsConsent?: boolean; analytics?: boolean }

const loadSession = () => api.get<Session>('/auth/me').catch((): Session => ({ user: null }))

/** Loads the session once (`GET /api/auth/me`); the cookie itself is httpOnly and never read here. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null | undefined>(undefined)
  const [needsConsent, setNeedsConsent] = useState(false)
  // The account the session flags (consent, analytics) were last loaded for.
  const flagsFor = useRef<string | null>(null)

  const applyFlags = useCallback((s: Session) => {
    flagsFor.current = s.user?.id ?? null
    setNeedsConsent(!!s.user && !!s.needsConsent)
    void syncAnalytics(s.user?.id ?? null, !!s.analytics)
  }, [])

  const apply = useCallback(
    (s: Session) => {
      setUser(s.user)
      applyFlags(s)
    },
    [applyFlags],
  )

  const refresh = useCallback(async () => apply(await loadSession()), [apply])

  const acceptConsent = useCallback(async () => {
    await api.post('/me/consents/legal')
    setNeedsConsent(false)
  }, [])

  const logout = useCallback(async () => {
    await api.post('/auth/logout')
    setUser(null)
    void syncAnalytics(null, false)
  }, [])

  useEffect(() => {
    let live = true
    void loadSession().then((s) => live && apply(s))
    return () => {
      live = false
    }
  }, [apply])

  // Sign-in screens set the user directly; load that account's flags (not the user) too.
  const userId = user?.id
  useEffect(() => {
    if (!userId || userId === flagsFor.current) return
    flagsFor.current = userId
    void loadSession().then((s) => s.user?.id === userId && applyFlags(s))
  }, [userId, applyFlags])

  const value = useMemo(
    () => ({ user, setUser, refresh, logout, needsConsent, acceptConsent }),
    [user, refresh, logout, needsConsent, acceptConsent],
  )
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
