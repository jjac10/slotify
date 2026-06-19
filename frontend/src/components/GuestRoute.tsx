import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

/** Rutas solo para invitados (login/registro): si ya hay sesión, fuera → "/". */
export function GuestRoute() {
  const { status } = useAuth()
  if (status === 'loading') return null
  if (status === 'authenticated') return <Navigate to="/" replace />
  return <Outlet />
}
