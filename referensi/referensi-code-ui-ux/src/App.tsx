import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { DriverApp } from '@/features/driver/DriverApp'
import { OperatorApp } from '@/features/operator/OperatorApp'
import { useSimClock } from '@/store/derived'

function Clock() {
  useSimClock()
  return null
}

export default function App() {
  return (
    <BrowserRouter>
      <Clock />
      <div className="h-screen">
        <Routes>
          <Route path="/ops/*" element={<OperatorApp />} />
          <Route path="/*" element={<DriverApp />} />
        </Routes>
      </div>
    </BrowserRouter>
  )
}
