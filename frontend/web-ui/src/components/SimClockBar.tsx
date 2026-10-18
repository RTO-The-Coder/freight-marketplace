import { useEffect, useRef, useState } from 'react'
import { advanceSummary, amountToTicks, useSimClock, type AdvanceUnit } from '@freight/fleetmanagement-core'
import { fmtSimDateTime, isoToDatetimeLocal } from '@freight/fleetmanagement-core'
import { Modal } from './Modal'

/** What the clock popup shows: work in progress, the result for a few seconds, or an error until closed. */
type ClockNotice =
  | { kind: 'progress'; text: string }
  | { kind: 'done'; title: string; detail: string | null }
  | { kind: 'error'; text: string }

/** What the user asked for - kept here (not in useSimClock) so the popup can say it. */
type ClockAction = { kind: 'advance'; label: string } | { kind: 'set' }

const RESULT_VISIBLE_MS = 3000

/**
 * The simulation clock, with Advance and Set time - in the header of both web apps. The bar never
 * changes size: progress, the result and errors show in a popup over the page.
 */
export function SimClockBar() {
  const { currentTime, busy, lastAdvance, error, advance, setTime } = useSimClock()

  const [amount, setAmount] = useState('1')
  const [unit, setUnit] = useState<AdvanceUnit>('ticks')

  const [showSet, setShowSet] = useState(false)
  const [setValue, setSetValue] = useState('')

  const [notice, setNotice] = useState<ClockNotice | null>(null)
  const action = useRef<ClockAction | null>(null)
  const closeTimer = useRef<number | undefined>(undefined)

  // When a request finishes (busy goes false), show its result or its error. An error with no
  // request of ours (reading the clock failed) shows too.
  useEffect(() => {
    if (busy) return
    const finished = action.current
    action.current = null
    window.clearTimeout(closeTimer.current)

    if (error) {
      setNotice({ kind: 'error', text: error })
      return
    }
    if (!finished) return

    setNotice(
      finished.kind === 'advance'
        ? {
            kind: 'done',
            title: `Now ${fmtSimDateTime(currentTime)}`,
            detail: lastAdvance ? advanceSummary(lastAdvance) : null,
          }
        : { kind: 'done', title: `Clock set to ${fmtSimDateTime(currentTime)}`, detail: null },
    )
    closeTimer.current = window.setTimeout(() => setNotice(null), RESULT_VISIBLE_MS)
    // Only the end of a request decides what to show; the values are read as they are then.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busy, error])

  useEffect(() => () => window.clearTimeout(closeTimer.current), [])

  const ticks = amountToTicks(amount, unit)

  const handleAdvance = () => {
    if (ticks <= 0 || busy) return
    const n = Number(amount)
    const label = `${n} ${unit === 'hours' ? (n === 1 ? 'hour' : 'hours') : n === 1 ? 'tick' : 'ticks'}`
    action.current = { kind: 'advance', label }
    window.clearTimeout(closeTimer.current)
    setNotice({ kind: 'progress', text: `Advancing the clock by ${label}…` })
    void advance(ticks)
  }

  const openSet = () => {
    setSetValue(isoToDatetimeLocal(currentTime))
    setShowSet(true)
  }

  const applySet = () => {
    if (!setValue) return
    setShowSet(false)
    action.current = { kind: 'set' }
    window.clearTimeout(closeTimer.current)
    setNotice({ kind: 'progress', text: 'Setting the clock…' })
    void setTime(setValue)
  }

  return (
    <div className="simclock">
      <span className="simclock__label">
        <span className="simclock__icon" aria-hidden="true">
          ◷
        </span>
        Sim
      </span>
      <span className="simclock__time">{fmtSimDateTime(currentTime)}</span>

      <span className="simclock__advance">
        <input
          type="number"
          min={1}
          step={1}
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') handleAdvance()
          }}
          aria-label="Amount to advance"
        />
        <select value={unit} onChange={(e) => setUnit(e.target.value as AdvanceUnit)} aria-label="Unit">
          <option value="ticks">ticks</option>
          <option value="hours">hours</option>
        </select>
        <button
          type="button"
          className="btn btn--sm btn--primary"
          disabled={busy || ticks <= 0}
          onClick={handleAdvance}
        >
          Advance
        </button>
        <button type="button" className="btn btn--sm btn--primary" disabled={busy} onClick={openSet}>
          Set time
        </button>
      </span>

      {notice && (
        <div
          className={`simclock-popup simclock-popup--${notice.kind}`}
          role={notice.kind === 'error' ? 'alert' : 'status'}
        >
          {notice.kind === 'progress' && <span className="simclock-popup__spinner" aria-hidden="true" />}
          {notice.kind === 'done' && (
            <span className="simclock-popup__icon" aria-hidden="true">
              ✓
            </span>
          )}
          {notice.kind === 'error' && (
            <span className="simclock-popup__icon" aria-hidden="true">
              ⚠
            </span>
          )}
          <span className="simclock-popup__text">
            {notice.kind === 'done' ? (
              <>
                <strong>{notice.title}</strong>
                {notice.detail && <span>{notice.detail}</span>}
              </>
            ) : (
              notice.text
            )}
          </span>
          {notice.kind === 'error' && (
            <button type="button" className="simclock-popup__close" onClick={() => setNotice(null)} aria-label="Close">
              ×
            </button>
          )}
        </div>
      )}

      {showSet && (
        <Modal title="Set simulation time" onClose={() => setShowSet(false)}>
          <div className="stack">
            <label className="field">
              <span>Simulation time</span>
              <input type="datetime-local" value={setValue} onChange={(e) => setSetValue(e.target.value)} />
            </label>
            <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-subtle)', margin: 0 }}>
              Jumps the clock without simulating movement. Reset in-flight trips afterward.
            </p>
          </div>
          <div className="modal-actions">
            <button type="button" className="btn" onClick={() => setShowSet(false)}>
              Cancel
            </button>
            <button
              type="button"
              className="btn btn--primary"
              onClick={applySet}
              disabled={busy || !setValue}
            >
              Set time
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
