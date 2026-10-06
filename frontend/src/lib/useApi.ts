import { useCallback, useEffect, useState } from 'react'
import { api, errorCode } from './api'

export type ApiState<T> = {
  data: T | undefined
  /** Error code when the last load failed. */
  error: string | null
  loading: boolean
  reload: () => void
  /** Replace the data after a mutation that returned the new value. */
  setData: (data: T) => void
}

/** GETs `path` (skipped while null) and reloads when it changes. */
export function useApi<T>(path: string | null): ApiState<T> {
  const [state, setState] = useState<{ path: string | null; data?: T; error: string | null }>({ path: null, error: null })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (path === null) return
    const ctrl = new AbortController()
    api
      .get<T>(path, ctrl.signal)
      .then((data) => setState({ path, data, error: null }))
      .catch((err: unknown) => {
        if (!ctrl.signal.aborted) setState({ path, error: errorCode(err) })
      })
    return () => ctrl.abort()
  }, [path, version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])
  const setData = useCallback((data: T) => setState((s) => ({ ...s, data, error: null })), [])
  const current = state.path === path

  return {
    data: current ? state.data : undefined,
    error: current ? state.error : null,
    loading: path !== null && !current,
    reload,
    setData,
  }
}
