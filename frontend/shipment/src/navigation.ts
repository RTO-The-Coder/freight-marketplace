/** The page shown: the Shippers list (shipperId null) or one shipper's page. */
export interface View {
  shipperId: string | null
}

/**
 * The address bar names the page, like the fleet web app's: `/` is the Shippers list,
 * `/?shipper=<id>` a shipper. So a refresh stays on the page, the address can be
 * bookmarked, and the browser's Back/Forward move between pages.
 */
export function viewFromLocation(): View {
  return { shipperId: new URLSearchParams(window.location.search).get('shipper') }
}
