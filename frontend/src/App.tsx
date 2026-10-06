import { Route, Routes } from 'react-router'
import { DesignSystemPage } from './pages/DesignSystemPage'
import { StatusPage } from './pages/StatusPage'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<StatusPage />} />
      <Route path="/design" element={<DesignSystemPage />} />
    </Routes>
  )
}
