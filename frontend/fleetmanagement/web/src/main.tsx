import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { SimClockProvider } from '@freight/fleetmanagement-core'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <SimClockProvider>
      <App />
    </SimClockProvider>
  </StrictMode>,
)
