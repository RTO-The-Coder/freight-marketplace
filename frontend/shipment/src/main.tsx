import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@freight/web-ui/src/styles/tokens.css'
import '@freight/web-ui/src/styles/components.css'
import '@freight/web-ui/src/styles/columns.css'
import './apiClient'
import App from './App.tsx'
import { SimClockProvider } from '@freight/fleetmanagement-core'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SimClockProvider>
      <App />
    </SimClockProvider>
  </StrictMode>,
)
