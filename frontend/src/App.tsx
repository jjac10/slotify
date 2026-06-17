import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ReserveFlowPage } from './pages/ReserveFlowPage'
import { MyReservationsPage } from './pages/MyReservationsPage'
import { OwnerAgendaPage } from './pages/OwnerAgendaPage'

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Navigate to="/reservar" replace />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="reservar" element={<ReserveFlowPage />} />

        {/* Rutas que requieren sesión. */}
        <Route element={<ProtectedRoute />}>
          <Route path="mis-reservas" element={<MyReservationsPage />} />
          <Route path="agenda" element={<OwnerAgendaPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/reservar" replace />} />
      </Route>
    </Routes>
  )
}
