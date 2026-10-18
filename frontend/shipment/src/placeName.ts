/** "Main warehouse — 1100 Favoriten, Vienna" -> name and area; a name without " — " is all name. */
export function splitPlaceName(placeName: string): { name: string; area: string | null } {
  const at = placeName.indexOf(' — ')
  return at < 0 ? { name: placeName, area: null } : { name: placeName.slice(0, at), area: placeName.slice(at + 3) }
}
