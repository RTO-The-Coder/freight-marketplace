import type { DriverSummaryDto } from '@freight/api-client'
import { useMemo, useRef, useState } from 'react'
import { fullName } from './driverFormat'

interface DriverSearchSelectProps {
  label: string
  drivers: DriverSummaryDto[]
  /** Currently selected driver id, or null. */
  value: string | null
  onChange: (driverId: string | null) => void
  /** Exclude this driver id from results (the other slot's pick). */
  excludeId?: string | null
  disabled?: boolean
}

const MAX_RESULTS = 6

/**
 * Searchable single-select for picking a driver from a large pool. Type a name
 * (first or last), pick from the dropdown; the selection collapses to a chip
 * with a Change action. The dropdown always opens downward and floats over the
 * content below, so opening it never shifts the modal's layout.
 */
export function DriverSearchSelect({
  label,
  drivers,
  value,
  onChange,
  excludeId = null,
  disabled = false,
}: DriverSearchSelectProps) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState(false)
  const blurTimer = useRef<number | undefined>(undefined)

  const selected = value ? drivers.find((d) => d.driverId === value) ?? null : null

  const results = useMemo(() => {
    const q = query.trim().toLowerCase()
    const pool = drivers.filter((d) => d.driverId !== excludeId)
    const matched = q ? pool.filter((d) => fullName(d).toLowerCase().includes(q)) : pool
    return { list: matched.slice(0, MAX_RESULTS), total: matched.length }
  }, [drivers, excludeId, query])

  if (selected) {
    return (
      <div className="field">
        <span>{label}</span>
        <div className="driver-select__chosen">
          <span className="driver-select__name">{fullName(selected)}</span>
          <button
            type="button"
            className="btn btn--ghost btn--sm"
            onClick={() => {
              onChange(null)
              setQuery('')
            }}
            disabled={disabled}
          >
            Change
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="field">
      <span>{label}</span>
      <div className="driver-select">
        <input
          type="text"
          placeholder="Search by name…"
          value={query}
          disabled={disabled}
          role="combobox"
          aria-expanded={open}
          onChange={(e) => {
            setQuery(e.target.value)
            setOpen(true)
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => {
            blurTimer.current = window.setTimeout(() => setOpen(false), 120)
          }}
        />

        {open && (
          <ul className="driver-select__menu">
            {results.list.length === 0 ? (
              <li className="driver-select__empty">
                {query.trim() ? 'No drivers match' : 'No unassigned drivers'}
              </li>
            ) : (
              results.list.map((d) => (
                <li key={d.driverId}>
                  <button
                    type="button"
                    className="driver-select__option"
                    onMouseDown={(e) => {
                      e.preventDefault()
                      window.clearTimeout(blurTimer.current)
                      onChange(d.driverId)
                      setOpen(false)
                    }}
                  >
                    {fullName(d)}
                  </button>
                </li>
              ))
            )}
            {results.total > results.list.length && (
              <li className="driver-select__more">+{results.total - results.list.length} more — keep typing</li>
            )}
          </ul>
        )}
      </div>
    </div>
  )
}
