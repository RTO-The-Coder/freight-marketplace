import { companyInitials, companyLogoHues } from '@freight/fleetmanagement-core'

interface CompanyLogoProps {
  name: string
  size?: 'md' | 'lg'
}

/**
 * A generated monogram tile — no image files. Colours and initials come from
 * core so web and mobile render the same mark for the same company.
 */
export function CompanyLogo({ name, size = 'md' }: CompanyLogoProps) {
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
