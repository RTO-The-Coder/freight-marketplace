interface PickerProps<T extends string> {
  label: string
  options: readonly T[]
  value: T | null
  onChange: (value: T) => void
  /** The option in plain words ("Box van" for BoxVan); the value itself when left out. */
  labelOf?: (value: T) => string
  /**
   * A short description under each option ("closed, dry goods", "24,000 kg · 90 m³"). Given,
   * the options are drawn as cards side by side.
   */
  description?: (value: T) => string
  /** Optional descriptive text shown under the selected option. */
  hint?: (value: T) => string
}

/** Click-to-select list — the app's replacement for dropdowns everywhere. */
export function Picker<T extends string>({ label, options, value, onChange, labelOf, description, hint }: PickerProps<T>) {
  return (
    <div className="field" role="group" aria-label={label}>
      <span>{label}</span>
      <ul className={description ? 'option-cards' : 'picker'}>
        {options.map((option) => (
          <li key={option}>
            <button
              type="button"
              className={
                description ? 'option-card' : `picker__item${option === value ? ' picker__item--selected' : ''}`
              }
              aria-pressed={option === value}
              onClick={() => onChange(option)}
            >
              {description ? (
                <>
                  <span className="option-card__title">{labelOf ? labelOf(option) : option}</span>
                  <span className="option-card__description">{description(option)}</span>
                </>
              ) : labelOf ? (
                labelOf(option)
              ) : (
                option
              )}
            </button>
          </li>
        ))}
      </ul>
      {hint && value && <p className="hint">{hint(value)}</p>}
    </div>
  )
}
