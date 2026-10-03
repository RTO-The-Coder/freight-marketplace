/**
 * A company's monogram colours: two hues derived deterministically from its name
 * (FNV-1a hash → hue), so the same company always gets the same mark.
 */
export function companyLogoHues(name: string): { hue: number; hue2: number } {
  const hue = fnv1a(name) % 360
  return { hue, hue2: (hue + 40) % 360 }
}

/** First letters of up to the first two words, upper-cased; '' for a blank name. */
export function companyInitials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => word[0]?.toUpperCase() ?? '')
    .join('')
}

function fnv1a(input: string): number {
  let hash = 0x811c9dc5
  for (let i = 0; i < input.length; i++) {
    hash ^= input.charCodeAt(i)
    hash = Math.imul(hash, 0x01000193)
  }
  return hash >>> 0
}
