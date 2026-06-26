import { Navigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { LandingPage } from '../pages/LandingPage'

/**
 * Ruta "/". Anónimo → landing pública. Logueado → su home según rol:
 * cliente → Mi Slotify; owner → Panel. (Nunca se ve la landing logueado.)
 */
export function HomeRoute() {
  const { status, isOwner } = useAuth()
  if (status === 'loading') return null
  if (status === 'anonymous') return <LandingPage />
  return <Navigate to={isOwner ? '/panel' : '/inicio'} replace />
}
