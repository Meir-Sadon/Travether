import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api } from '../lib/api'
import type { Me } from './types'
import { AuthContext } from './useAuth'

const loadSession = () =>
  api
    .get<{ user: Me | null }>('/auth/me')
    .then((s) => s.user)
    .catch(() => null)

/** Loads the session once (`GET /api/auth/me`); the cookie itself is httpOnly and never read here. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Me | null | undefined>(undefined)

  const refresh = useCallback(async () => setUser(await loadSession()), [])

  const logout = useCallback(async () => {
    await api.post('/auth/logout')
    setUser(null)
  }, [])

  useEffect(() => {
    let live = true
    void loadSession().then((u) => live && setUser(u))
    return () => {
      live = false
    }
  }, [])

  const value = useMemo(() => ({ user, setUser, refresh, logout }), [user, refresh, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
