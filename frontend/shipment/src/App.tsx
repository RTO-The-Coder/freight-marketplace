import { useEffect, useState } from 'react'
import './App.css'
import { SimClockBar } from '@freight/web-ui'
import { ShipperDetailScreen } from './screens/ShipperDetailScreen'
import { ShippersScreen } from './screens/ShippersScreen'
import { type View, viewFromLocation } from './navigation'

function App() {
  const [view, setView] = useState<View>(viewFromLocation)

  // Keep the address in step with the page.
  useEffect(() => {
    const current = new URLSearchParams(window.location.search).get('shipper')
    if (current === view.shipperId) return
    window.history.pushState(null, '', view.shipperId ? `?shipper=${view.shipperId}` : window.location.pathname)
  }, [view.shipperId])

  // Browser Back/Forward: show the page the address now names.
  useEffect(() => {
    const onPopState = () => setView(viewFromLocation())
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  return (
    // The shipper page's three columns use the whole window, like the fleet company page.
    <div className={view.shipperId ? 'app app--wide' : 'app'}>
      {/* Same header bar as the fleet web app, with the same simulation clock. */}
      <header className="app-header">
        <span className="app-header__title">
          Freight Marketplace <span className="app-header__tag">demo</span>
        </span>
        <SimClockBar />
      </header>

      {/* No breadcrumb or back link: the Shippers list is the plain address (or browser Back). */}
      {view.shipperId === null && <ShippersScreen onSelect={(shipperId) => setView({ shipperId })} />}
      {view.shipperId !== null && <ShipperDetailScreen shipperId={view.shipperId} />}
    </div>
  )
}

export default App
