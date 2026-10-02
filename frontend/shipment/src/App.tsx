import { useState } from 'react'
import './App.css'
import { ShipperDetailScreen } from './screens/ShipperDetailScreen'
import { ShippersScreen } from './screens/ShippersScreen'
import type { View } from './navigation'

function App() {
  const [view, setView] = useState<View>({ section: 'shippers', shipperId: null })

  return (
    <div className="app">
      <h1>Freight Marketplace</h1>

      {view.shipperId === null && (
        <ShippersScreen onSelect={(shipperId) => setView({ section: 'shippers', shipperId })} />
      )}
      {view.shipperId !== null && (
        <ShipperDetailScreen
          shipperId={view.shipperId}
          onBack={() => setView({ section: 'shippers', shipperId: null })}
        />
      )}
    </div>
  )
}

export default App
