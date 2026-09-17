import { useState } from 'react'
import './App.css'
import { Chevron } from './components/Chevron'
import { SimClockBar } from './components/SimClockBar'
import { useSimClock } from './SimClock'
import { DriverDetailScreen } from './screens/DriverDetailScreen'
import { TruckDetailScreen } from './screens/TruckDetailScreen'
import { TruckingCompaniesScreen } from './screens/TruckingCompaniesScreen'
import { TruckingCompanyDetailScreen } from './screens/TruckingCompanyDetailScreen'
import type { View } from './navigation'

function initialView(): View {
  // Dev convenience: deep-link with ?company=<id> or ?truck=<id>&company=<id>.
  const params = new URLSearchParams(window.location.search)
  const companyId = params.get('company')
  const truckId = params.get('truck')
  if (truckId && companyId) {
    return { screen: 'truck', truckId, truckName: null, companyId, companyName: null }
  }
  return companyId ? { screen: 'company', companyId, companyName: null } : { screen: 'companies' }
}

function App() {
  const [view, setView] = useState<View>(initialView)
  const { simVersion } = useSimClock()

  /** Screens report a name they've loaded so the breadcrumb can show it. */
  const setCompanyName = (companyName: string) =>
    setView((v) => ('companyName' in v && v.companyName !== companyName ? { ...v, companyName } : v))
  const setTruckName = (truckName: string) =>
    setView((v) => ('truckName' in v && v.truckName !== truckName ? { ...v, truckName } : v))
  const setDriverName = (driverName: string) =>
    setView((v) => (v.screen === 'driver' && v.driverName !== driverName ? { ...v, driverName } : v))

  return (
    <div className="app">
      <header className="app-header">
        <span className="app-header__title">
          Freight Marketplace <span className="app-header__tag">demo</span>
        </span>
        <SimClockBar />
      </header>

      {view.screen !== 'companies' && <Crumbs view={view} navigate={setView} />}

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
          onSelectDriver={(driverId, driverName) =>
            setView({
              screen: 'driver',
              driverId,
              driverName,
              truckId: view.truckId,
              truckName: view.truckName,
              companyId: view.companyId,
              companyName: view.companyName,
            })
          }
        />
      )}

      {view.screen === 'driver' && (
        <DriverDetailScreen driverId={view.driverId} simVersion={simVersion} onDriverLoaded={setDriverName} />
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
  const toCompany = (v: Extract<View, { screen: 'company' | 'truck' | 'driver' }>) =>
    navigate({ screen: 'company', companyId: v.companyId, companyName: v.companyName })
  const toTruck = (v: Extract<View, { screen: 'truck' | 'driver' }>) =>
    navigate({
      screen: 'truck',
      truckId: v.truckId,
      truckName: v.truckName,
      companyId: v.companyId,
      companyName: v.companyName,
    })

  const trail: Crumb[] = [{ label: 'Companies', go: toCompanies }]

  if (view.screen === 'company') {
    trail.push({ label: view.companyName ?? 'Company' })
  } else if (view.screen === 'truck') {
    trail.push({ label: view.companyName ?? 'Company', go: () => toCompany(view) })
    trail.push({ label: view.truckName ?? 'Truck' })
  } else if (view.screen === 'driver') {
    trail.push({ label: view.companyName ?? 'Company', go: () => toCompany(view) })
    trail.push({ label: view.truckName ?? 'Truck', go: () => toTruck(view) })
    trail.push({ label: view.driverName ?? 'Driver' })
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
