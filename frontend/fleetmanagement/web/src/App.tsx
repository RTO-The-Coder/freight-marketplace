import { useEffect, useState } from 'react'
import './App.css'
import { Chevron } from './components/Chevron'
import { SimClockBar } from './components/SimClockBar'
import { useSimClock } from '@freight/fleetmanagement-core'
import { TruckDetailScreen } from './screens/TruckDetailScreen'
import { TruckingCompaniesScreen } from './screens/TruckingCompaniesScreen'
import { TruckingCompanyDetailScreen } from './screens/TruckingCompanyDetailScreen'
import { configureApi, type View } from '@freight/fleetmanagement-core'

// Module top level, not inside a component or effect: must run before any screen fetches.
configureApi(import.meta.env.VITE_API_BASE_URL as string)

/**
 * The address bar always names the page: `/` is the Companies list, `/?company=<id>` a
 * company, `/?company=<id>&truck=<id>` a truck. So a refresh stays on the page, the address
 * can be bookmarked, and the browser's Back/Forward move between pages.
 */
function viewFromLocation(): View {
  const params = new URLSearchParams(window.location.search)
  const companyId = params.get('company')
  const truckId = params.get('truck')
  if (truckId && companyId) {
    return { screen: 'truck', truckId, truckName: null, companyId, companyName: null }
  }
  return companyId ? { screen: 'company', companyId, companyName: null } : { screen: 'companies' }
}

/** The `company` / `truck` address parameters for a view (none for the Companies list). */
function locationParams(view: View): { company: string | null; truck: string | null } {
  if (view.screen === 'company') return { company: view.companyId, truck: null }
  if (view.screen === 'truck') return { company: view.companyId, truck: view.truckId }
  return { company: null, truck: null }
}

function App() {
  const [view, setView] = useState<View>(viewFromLocation)
  const { simVersion } = useSimClock()

  // Keep the address in step with the page. Only company/truck are compared, so other
  // parameters (e.g. the ?panel=assign dev shortcut) survive the first render.
  const { company, truck } = locationParams(view)
  useEffect(() => {
    const params = new URLSearchParams(window.location.search)
    if (params.get('company') === company && params.get('truck') === truck) return
    const next = new URLSearchParams()
    if (company) next.set('company', company)
    if (truck) next.set('truck', truck)
    const search = next.toString()
    window.history.pushState(null, '', search ? `?${search}` : window.location.pathname)
  }, [company, truck])

  // Browser Back/Forward: show the page the address now names.
  useEffect(() => {
    const onPopState = () => setView(viewFromLocation())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  /** Screens report a name they've loaded so the breadcrumb can show it. */
  const setCompanyName = (companyName: string) =>
    setView((v) => ('companyName' in v && v.companyName !== companyName ? { ...v, companyName } : v))
  const setTruckName = (truckName: string) =>
    setView((v) => ('truckName' in v && v.truckName !== truckName ? { ...v, truckName } : v))

  return (
    // The company page's three columns need more room than the other pages.
    <div className={view.screen === 'company' ? 'app app--wide' : 'app'}>
      <header className="app-header">
        <span className="app-header__title">
          Freight Marketplace <span className="app-header__tag">demo</span>
        </span>
        <SimClockBar />
      </header>

      {/* The company page has no breadcrumb: the Companies list is the plain address (or browser Back). */}
      {view.screen !== 'companies' && view.screen !== 'company' && <Crumbs view={view} navigate={setView} />}

      {view.screen === 'companies' && (
        <TruckingCompaniesScreen
          onSelect={(companyId, companyName) => setView({ screen: 'company', companyId, companyName })}
        />
      )}

      {view.screen === 'company' && (
        <TruckingCompanyDetailScreen
          companyId={view.companyId}
          simVersion={simVersion}
          onCompanyLoaded={setCompanyName}
          onSelectTruck={(truckId, truckName) =>
            setView({
              screen: 'truck',
              truckId,
              truckName,
              companyId: view.companyId,
              companyName: view.companyName,
            })
          }
        />
      )}

      {view.screen === 'truck' && (
        <TruckDetailScreen
          truckId={view.truckId}
          simVersion={simVersion}
          onTruckLoaded={setTruckName}
        />
      )}
    </div>
  )
}

interface Crumb {
  label: string
  go?: () => void
}

function Crumbs({ view, navigate }: { view: View; navigate: (v: View) => void }) {
  const toCompanies = () => navigate({ screen: 'companies' })
  const toCompany = (v: Extract<View, { screen: 'company' | 'truck' }>) =>
    navigate({ screen: 'company', companyId: v.companyId, companyName: v.companyName })

  const trail: Crumb[] = [{ label: 'Companies', go: toCompanies }]

  if (view.screen === 'company') {
    trail.push({ label: view.companyName ?? 'Company' })
  } else if (view.screen === 'truck') {
    trail.push({ label: view.companyName ?? 'Company', go: () => toCompany(view) })
    trail.push({ label: view.truckName ?? 'Truck' })
  }

  // The immediate parent (last crumb that has a `go`) is also the "back" target.
  const parent = [...trail].reverse().find((c) => c.go)

  return (
    <div className="nav">
      {parent && (
        <button type="button" className="nav__back" onClick={parent.go}>
          <Chevron direction="left" />
          {parent.label}
        </button>
      )}
      <nav className="crumbs" aria-label="Breadcrumb">
        {trail.map((crumb, i) => (
          <span key={i} style={{ display: 'contents' }}>
            {i > 0 && <span className="crumbs__sep">/</span>}
            {crumb.go ? (
              <button type="button" onClick={crumb.go}>
                {crumb.label}
              </button>
            ) : (
              <span className="crumbs__current" aria-current="page">
                {crumb.label}
              </span>
            )}
          </span>
        ))}
      </nav>
    </div>
  )
}

export default App
