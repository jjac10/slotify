import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LandingPage } from './pages/LandingPage'
import { ExplorePage } from './pages/ExplorePage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ReserveFlowPage } from './pages/ReserveFlowPage'
import { MyReservationsPage } from './pages/MyReservationsPage'
import { OwnerAgendaPage } from './pages/OwnerAgendaPage'
import { DashboardPage } from './pages/DashboardPage'
import { MyBusinessPage } from './pages/MyBusinessPage'
import { BusinessHoursPage } from './pages/BusinessHoursPage'

export function App() {
  return (
    <Routes>
      {/* Landing pública, sin el chrome de la app */}
      <Route path="/" element={<LandingPage />} />

      <Route element={<Layout />}>
        <Route path="explorar" element={<ExplorePage />} />
        <Route path="login" element={<LoginPage />} />
        <Route path="register" element={<RegisterPage />} />
        <Route path="reservar" element={<ReserveFlowPage />} />

        {/* Rutas que requieren sesión. */}
        <Route element={<ProtectedRoute />}>
          <Route path="mis-reservas" element={<MyReservationsPage />} />
          <Route path="mi-negocio" element={<MyBusinessPage />} />
          <Route path="horario" element={<BusinessHoursPage />} />
          <Route path="agenda" element={<OwnerAgendaPage />} />
          <Route path="panel" element={<DashboardPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
