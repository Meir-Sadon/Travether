import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import './styles/tokens.css'
import './styles/base.css'
import './i18n'
import './screens/screens.css'
import App from './App.tsx'
import { registerServiceWorker } from './lib/push'
import { startErrorTracking } from './lib/telemetry'

registerServiceWorker()
void startErrorTracking()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
