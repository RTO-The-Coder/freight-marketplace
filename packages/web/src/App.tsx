import { useState } from 'react'
import './App.css'
import { DriverDetailScreen } from './screens/DriverDetailScreen'
import { DriversScreen } from './screens/DriversScreen'
import { TruckDetailScreen } from './screens/TruckDetailScreen'
import { TrucksScreen } from './screens/TrucksScreen'
import { TruckingCompaniesScreen } from './screens/TruckingCompaniesScreen'
import { TruckingCompanyDetailScreen } from './screens/TruckingCompanyDetailScreen'
import type { View } from './navigation'

type Section = View['section']

function App() {
  const [view, setView] = useState<View | null>(null)

  const goHome = () => setView(null)

  const openSection = (section: Section) => {
    if (section === 'companies') setView({ section: 'companies', companyId: null })
    if (section === 'trucks') setView({ section: 'trucks', truckId: null })
    if (section === 'drivers') setView({ section: 'drivers', driverId: null })
  }

  return (
    <div className="app">
      <h1>Freight Marketplace</h1>

      {view === null && (
        <nav className="landing-menu">
          <button type="button" onClick={() => openSection('companies')}>
            Trucking Companies
          </button>
          <button type="button" onClick={() => openSection('trucks')}>
            Trucks
          </button>
          <button type="button" onClick={() => openSection('drivers')}>
            Drivers
          </button>
        </nav>
      )}

      {view !== null && (
        <button type="button" className="back-button" onClick={goHome}>
          ← Menu
        </button>
      )}

      {view?.section === 'companies' && view.companyId === null && (
        <TruckingCompaniesScreen onSelect={(companyId) => setView({ section: 'companies', companyId })} />
      )}
      {view?.section === 'companies' && view.companyId !== null && (
        <TruckingCompanyDetailScreen
          companyId={view.companyId}
          onBack={() => setView({ section: 'companies', companyId: null })}
          onSelectTruck={(truckId) => setView({ section: 'trucks', truckId })}
        />
      )}

      {view?.section === 'trucks' && view.truckId === null && (
        <TrucksScreen onSelect={(truckId) => setView({ section: 'trucks', truckId })} />
      )}
      {view?.section === 'trucks' && view.truckId !== null && (
        <TruckDetailScreen truckId={view.truckId} onBack={() => setView({ section: 'trucks', truckId: null })} />
      )}

      {view?.section === 'drivers' && view.driverId === null && (
        <DriversScreen onSelect={(driverId) => setView({ section: 'drivers', driverId })} />
      )}
      {view?.section === 'drivers' && view.driverId !== null && (
        <DriverDetailScreen driverId={view.driverId} onBack={() => setView({ section: 'drivers', driverId: null })} />
      )}
    </div>
  )
}

export default App
