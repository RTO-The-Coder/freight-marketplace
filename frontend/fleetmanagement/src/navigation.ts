/**
 * Navigation is a small stack of views. The app opens on the Trucking
 * Companies list; every other screen is reached by drilling in from there
 * (company → truck → driver). There is no landing menu and no standalone
 * global Trucks / Drivers list anymore — those pools live on a company.
 *
 * Each drill-in view carries the human-readable name of what it's showing
 * (and of its ancestors) so the breadcrumb and back link can name them
 * without a second fetch. Names are filled in as they become known — a view
 * pushed before its data loads carries `null` until the screen reports it.
 */
export type View =
  | { screen: 'companies' }
  | { screen: 'company'; companyId: string; companyName: string | null }
  | {
      screen: 'truck'
      truckId: string
      truckName: string | null
      companyId: string
      companyName: string | null
    }
  | {
      screen: 'driver'
      driverId: string
      driverName: string | null
      truckId: string
      truckName: string | null
      companyId: string
      companyName: string | null
    }
