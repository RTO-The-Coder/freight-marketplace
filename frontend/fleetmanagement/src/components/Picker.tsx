interface PickerProps<T extends string> {
  label: string
  options: readonly T[]
  value: T | null
  onChange: (value: T) => void
  /** Optional descriptive text shown under the selected option. */
  hint?: (value: T) => string
}

/** Click-to-select list — the app's replacement for dropdowns everywhere. */
export function Picker<T extends string>({ label, options, value, onChange, hint }: PickerProps<T>) {
  return (
    <div className="field">
      <span>{label}</span>
      <ul className="picker">
        {options.map((option) => (
          <li key={option}>
            <button
              type="button"
              className={`picker__item${option === value ? ' picker__item--selected' : ''}`}
              onClick={() => onChange(option)}
            >
              {option}
            </button>
          </li>
        ))}
      </ul>
      {hint && value && <span style={{ color: 'var(--c-text-subtle)' }}>{hint(value)}</span>}
    </div>
  )
}
