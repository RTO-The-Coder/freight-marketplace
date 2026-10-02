interface CompanyLogoProps {
  name: string
  size?: 'md' | 'lg'
}

/**
 * A generated monogram tile — no image files. The gradient's two hues are
 * derived deterministically from the company name (FNV-1a hash → hue), so the
 * same company always renders the same mark and any two companies look
 * distinct. Initials are the first letters of up to the first two words.
 */
export function CompanyLogo({ name, size = 'md' }: CompanyLogoProps) {
  const hash = fnv1a(name)
  const hue = hash % 360
  const hue2 = (hue + 40) % 360

  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')

  return (
    <span
      className={`logo logo--${size}`}
      aria-hidden="true"
      style={{
        background: `linear-gradient(135deg, hsl(${hue} 68% 52%), hsl(${hue2} 64% 44%))`,
      }}
    >
      {initials || '?'}
    </span>
  )
}

function fnv1a(input: string): number {
  let hash = 0x811c9dc5
  for (let i = 0; i < input.length; i++) {
    hash ^= input.charCodeAt(i)
    hash = Math.imul(hash, 0x01000193)
  }
  return hash >>> 0
}
