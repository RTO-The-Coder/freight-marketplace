import { useCallback, useEffect, useMemo, useState } from 'react'
import { errorMessage } from './ScreenState'

interface LoaderState<T> {
  data: T | null
  error: string | null
  /** The load this data came from; a newer one in progress means "reloading". */
  loadedFor: object | null
}

/**
 * Loads a screen's data and tells apart the first load (show placeholders) from a reload
 * (keep the content, faded, with "Updating"). Reloads when `load` or `reloadKey` changes
 * (e.g. the sim clock's version) and on `reload()` (pull-to-refresh, after an action).
 */
export function useLoader<T>(load: () => Promise<T>, reloadKey: unknown = null, fallbackError = 'Failed to load.') {
  const [tick, setTick] = useState(0)
  // A fresh token per load: compared with the one the data came from.
  const token = useMemo(() => ({ load, reloadKey, tick }), [load, reloadKey, tick])
  const [state, setState] = useState<LoaderState<T>>({ data: null, error: null, loadedFor: null })

  useEffect(() => {
    let cancelled = false
    token.load().then(
      (data) => {
        if (!cancelled) setState({ data, error: null, loadedFor: token })
      },
      (err: unknown) => {
        // A failed reload keeps the last data on screen, with the error above it.
        if (!cancelled) setState((s) => ({ ...s, error: errorMessage(err, fallbackError), loadedFor: token }))
      },
    )
    return () => {
      cancelled = true
    }
  }, [token, fallbackError])

  const reload = useCallback(() => setTick((t) => t + 1), [])

  return {
    data: state.data,
    error: state.error,
    /** Nothing to show yet: the first load is running. */
    loading: state.data === null && state.loadedFor !== token,
    /** Data on screen, a newer load running. */
    reloading: state.data !== null && state.loadedFor !== token,
    reload,
  }
}
