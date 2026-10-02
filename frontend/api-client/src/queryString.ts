/**
 * Builds a `?a=1&b=2` query string from a params object, skipping `undefined`
 * values, or `''` if every value is undefined.
 */
export function buildQuery(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}
