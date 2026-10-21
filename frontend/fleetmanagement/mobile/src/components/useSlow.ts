import { useEffect, useState } from 'react'

/** After this long a wait is "slow": the routing server allows about one call a second. */
export const SLOW_AFTER_MS = 8000

/** True once `active` has stayed true for 8 s; false again as soon as it ends. */
export function useSlow(active: boolean, afterMs = SLOW_AFTER_MS): boolean {
  const [timedOut, setTimedOut] = useState(false)

  useEffect(() => {
    if (!active) return
    const timer = setTimeout(() => setTimedOut(true), afterMs)
    return () => {
      clearTimeout(timer)
      setTimedOut(false)
    }
  }, [active, afterMs])

  return active && timedOut
}
