import { useEffect, useRef, useState } from 'react'
import { TICK_MINUTES, useSimClock } from '../SimClock'
import { fmtSimDateTime, isoToDatetimeLocal } from '../simTime'
import { Modal } from './Modal'

type Unit = 'ticks' | 'hours'

const TICKS_PER_HOUR = 60 / TICK_MINUTES // 12

export function SimClockBar() {
  const { currentTime, busy, lastAdvance, error, advance, setTime } = useSimClock()

  const [amount, setAmount] = useState('1')
  const [unit, setUnit] = useState<Unit>('ticks')

  const [showSet, setShowSet] = useState(false)
  const [setValue, setSetValue] = useState('')

  const [toast, setToast] = useState<string | null>(null)
  const toastTimer = useRef<number | undefined>(undefined)

  useEffect(() => {
    if (!lastAdvance) return
    const { tripsAdvanced, tripsCompleted } = lastAdvance
    const parts: string[] = []
    if (tripsAdvanced > 0) parts.push(`${tripsAdvanced} truck${tripsAdvanced === 1 ? '' : 's'} moved`)
    if (tripsCompleted > 0) parts.push(`${tripsCompleted} trip${tripsCompleted === 1 ? '' : 's'} completed`)
    setToast(parts.length ? parts.join(' · ') : 'Clock advanced')
    window.clearTimeout(toastTimer.current)
    toastTimer.current = window.setTimeout(() => setToast(null), 3200)
  }, [lastAdvance])

  const n = Number(amount)
  const ticks = Number.isFinite(n) && n > 0 ? Math.round(unit === 'hours' ? n * TICKS_PER_HOUR : n) : 0

  const handleAdvance = () => {
    if (ticks > 0) void advance(ticks)
  }

  const openSet = () => {
    setSetValue(isoToDatetimeLocal(currentTime))
    setShowSet(true)
  }

  const applySet = async () => {
    if (!setValue) return
    await setTime(setValue)
    setShowSet(false)
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
        <select value={unit} onChange={(e) => setUnit(e.target.value as Unit)} aria-label="Unit">
          <option value="ticks">ticks</option>
          <option value="hours">hours</option>
        </select>
        <button
          type="button"
          className="btn btn--sm btn--primary"
          disabled={busy || ticks <= 0}
          onClick={handleAdvance}
        >
          {busy ? 'Advancing…' : 'Advance'}
        </button>
        <button type="button" className="btn btn--sm btn--primary" disabled={busy} onClick={openSet}>
          Set time
        </button>
      </span>

      {error && <span className="simclock__error">{error}</span>}
      {toast && <span className="simclock__toast">{toast}</span>}

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
              {busy ? 'Setting…' : 'Set time'}
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
