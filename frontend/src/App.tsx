import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ReserveFlowPage } from './pages/ReserveFlowPage'
import { MyReservationsPage } from './pages/MyReservationsPage'
import { OwnerAgendaPage } from './pages/OwnerAgendaPage'
import { DashboardPage } from './pages/DashboardPage'
import { MyBusinessPage } from './pages/MyBusinessPage'

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
          <Route path="mi-negocio" element={<MyBusinessPage />} />
          <Route path="agenda" element={<OwnerAgendaPage />} />
          <Route path="panel" element={<DashboardPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/reservar" replace />} />
      </Route>
    </Routes>
  )
}
