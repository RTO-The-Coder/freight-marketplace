interface ChevronProps {
  direction?: 'right' | 'left'
}

/** Directional chevron — right for clickable list rows, left for "back". */
export function Chevron({ direction = 'right' }: ChevronProps) {
  const d = direction === 'right' ? 'M6 3.5 10.5 8 6 12.5' : 'M10 3.5 5.5 8 10 12.5'
  return (
    <svg
      className="row-card__chevron"
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      aria-hidden="true"
    >
      <path d={d} stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}
