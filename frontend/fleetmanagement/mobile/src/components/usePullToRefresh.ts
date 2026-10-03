import { useCallback, useState } from 'react'

/** State for a pull-to-refresh gesture around an async reload. */
export function usePullToRefresh(reload: () => Promise<unknown>) {
  const [refreshing, setRefreshing] = useState(false)
  const onRefresh = useCallback(async () => {
    setRefreshing(true)
    try {
      await reload()
    } finally {
      setRefreshing(false)
    }
  }, [reload])
  return { refreshing, onRefresh }
}
