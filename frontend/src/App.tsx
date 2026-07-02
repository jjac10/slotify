import { Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { GuestRoute } from './components/GuestRoute'
import { HomeRoute } from './components/HomeRoute'
import { ExplorePage } from './pages/ExplorePage'
import { MiSlotifyPage } from './pages/MiSlotifyPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ReserveFlowPage } from './pages/ReserveFlowPage'
import { MyReservationsPage } from './pages/MyReservationsPage'
import { MyReviewsPage } from './pages/MyReviewsPage'
import { AcceptInvitePage } from './pages/AcceptInvitePage'
import { ForgotPasswordPage } from './pages/ForgotPasswordPage'
import { ResetPasswordPage } from './pages/ResetPasswordPage'
import { OwnerAgendaPage } from './pages/OwnerAgendaPage'
import { DashboardPage } from './pages/DashboardPage'
import { BusinessSettingsPage } from './pages/BusinessSettingsPage'
import { LegalPage } from './pages/LegalPage'

export function App() {
  return (
    <Routes>
      {/* "/" — landing pública si anónimo; home según rol si hay sesión */}
      <Route path="/" element={<HomeRoute />} />

      <Route element={<Layout />}>
        <Route path="explorar" element={<ExplorePage />} />
        <Route path="reservar" element={<ReserveFlowPage />} />
        {/* Enlace de invitación de empleado (público): crear cuenta + entrar */}
        <Route path="invitacion/:token" element={<AcceptInvitePage />} />
        {/* Recuperación de contraseña (público): pedir enlace y restablecer */}
        <Route path="recuperar" element={<ForgotPasswordPage />} />
        <Route path="restablecer" element={<ResetPasswordPage />} />
        {/* Público: logueado ve sus reservas; invitado busca por teléfono/email */}
        <Route path="mis-reservas" element={<MyReservationsPage />} />
        {/* Páginas legales públicas (RGPD): /legal/terminos · /legal/privacidad · /legal/cookies */}
        <Route path="legal/:seccion" element={<LegalPage />} />
        <Route path="legal" element={<Navigate to="/legal/terminos" replace />} />

        {/* Solo invitados (si hay sesión, fuera) */}
        <Route element={<GuestRoute />}>
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
        </Route>

        {/* Requieren sesión */}
        <Route element={<ProtectedRoute />}>
          <Route path="inicio" element={<MiSlotifyPage />} />
          <Route path="mis-resenas" element={<MyReviewsPage />} />
          <Route path="configuracion" element={<BusinessSettingsPage />} />
          {/* Compatibilidad con rutas anteriores */}
          <Route path="mi-negocio" element={<Navigate to="/configuracion" replace />} />
          <Route path="horario" element={<Navigate to="/configuracion" replace />} />
          <Route path="agenda" element={<OwnerAgendaPage />} />
          <Route path="panel" element={<DashboardPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}
