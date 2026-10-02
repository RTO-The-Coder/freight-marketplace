import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { simulationApi } from './apiClient'
import { datetimeLocalToIso } from './simTime'

interface SimClockValue {
  /** ISO simulated time, or null until first loaded. */
  currentTime: string | null
  /** Bumped every time the clock advances — screens refetch on change. */
  simVersion: number
  /** True while an advance / set request is in flight. */
  busy: boolean
  /** Last advance result, for a transient toast. */
  lastAdvance: { tripsAdvanced: number; tripsCompleted: number } | null
  error: string | null
  advance: (ticks: number) => Promise<void>
  setTime: (isoLocal: string) => Promise<void>
  refresh: () => Promise<void>
}

const SimClockContext = createContext<SimClockValue | null>(null)

export const TICK_MINUTES = 5

export function SimClockProvider({ children }: { children: ReactNode }) {
  const [currentTime, setCurrentTime] = useState<string | null>(null)
  const [simVersion, setSimVersion] = useState(0)
  const [busy, setBusy] = useState(false)
  const [lastAdvance, setLastAdvance] = useState<SimClockValue['lastAdvance']>(null)
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      const { currentTime: t } = await simulationApi.getTime()
      setCurrentTime(t)
      setError(null)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not read the simulation clock.')
    }
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const advance = useCallback(async (ticks: number) => {
    if (ticks <= 0) return
    setBusy(true)
    setError(null)
    try {
      const res = await simulationApi.advance(ticks)
      setCurrentTime(res.currentTime)
      setLastAdvance({ tripsAdvanced: res.tripsAdvanced, tripsCompleted: res.tripsCompleted })
      setSimVersion((v) => v + 1)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not advance the simulation clock.')
    } finally {
      setBusy(false)
    }
  }, [])

  const setTime = useCallback(async (datetimeLocal: string) => {
    setBusy(true)
    setError(null)
    try {
      // <input type="datetime-local"> gives "2026-08-29T14:20" (no zone). Simulation
      // time is a timezone-less wall clock, so pin the input to UTC before sending —
      // 14:20 in the picker means 14:20 in the simulation, not the viewer's local zone.
      const res = await simulationApi.setTime(datetimeLocalToIso(datetimeLocal))
      setCurrentTime(res.currentTime)
      setLastAdvance(null)
      setSimVersion((v) => v + 1)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Could not set the simulation clock.')
    } finally {
      setBusy(false)
    }
  }, [])

  const value = useMemo<SimClockValue>(
    () => ({ currentTime, simVersion, busy, lastAdvance, error, advance, setTime, refresh }),
    [currentTime, simVersion, busy, lastAdvance, error, advance, setTime, refresh],
  )

  return <SimClockContext.Provider value={value}>{children}</SimClockContext.Provider>
}

export function useSimClock(): SimClockValue {
  const ctx = useContext(SimClockContext)
  if (!ctx) throw new Error('useSimClock must be used within a SimClockProvider')
  return ctx
}
