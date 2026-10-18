import { companyInitials, companyLogoHues } from '@freight/fleetmanagement-core'

interface LogoProps {
  name: string
  size?: 'md' | 'lg'
}

/**
 * A generated monogram tile for a trucking company or a shipper — no image files. Colours
 * and initials come from core so web and mobile render the same mark for the same name.
 */
export function Logo({ name, size = 'md' }: LogoProps) {
  const { hue, hue2 } = companyLogoHues(name)
  const initials = companyInitials(name)

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
