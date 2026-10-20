import { useEffect, useState } from 'react'

interface DateTimeFieldsProps {
  /** A `datetime-local` value, "2026-08-01T05:00"; '' when empty. */
  value: string
  /** Called with "YYYY-MM-DDTHH:mm", or '' while the date or the time is empty. */
  onChange: (value: string) => void
  /** Names the two inputs for screen readers: "{label} date", "{label} time". */
  label: string
  id?: string
  required?: boolean
  disabled?: boolean
}

const split = (value: string) => ({ date: value.slice(0, 10), time: value.slice(11, 16) })

/**
 * A date and a time as two fields side by side - a single `datetime-local` input is cut off in
 * narrow popups. Works on the same string, so callers keep `isoToDatetimeLocal` /
 * `datetimeLocalToIso`.
 */
export function DateTimeFields({ value, onChange, label, id, required, disabled }: DateTimeFieldsProps) {
  const [parts, setParts] = useState(() => split(value))

  // Follow a value set from outside (reset to now, a reload) - not our own half-typed one.
  useEffect(() => {
    setParts((current) => {
      const combined = current.date && current.time ? `${current.date}T${current.time}` : ''
      return combined === value ? current : split(value)
    })
  }, [value])

  const update = (next: { date: string; time: string }) => {
    setParts(next)
    onChange(next.date && next.time ? `${next.date}T${next.time}` : '')
  }

  return (
    <div className="datetime-fields">
      <input
        type="date"
        id={id}
        value={parts.date}
        onChange={(e) => update({ ...parts, date: e.target.value })}
        aria-label={`${label} date`}
        required={required}
        disabled={disabled}
      />
      <input
        type="time"
        id={id ? `${id}-time` : undefined}
        value={parts.time}
        onChange={(e) => update({ ...parts, time: e.target.value })}
        aria-label={`${label} time`}
        required={required}
        disabled={disabled}
      />
    </div>
  )
}
