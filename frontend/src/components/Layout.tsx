import { Link, Outlet } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

export function Layout() {
  const { user, status, isOwner, logout } = useAuth()
  const authenticated = status === 'authenticated'

  return (
    <div>
      <header>
        <Link to="/" style={{ fontWeight: 700 }}>
          Slotify
        </Link>
        <nav>
          <Link to="/reservar">Reservar</Link>
          {authenticated && (
            <Link to="/mis-reservas" data-testid="nav-my-reservations">
              Mis reservas
            </Link>
          )}
          {isOwner && (
            <Link to="/panel" data-testid="nav-dashboard">
              Panel
            </Link>
          )}
          {isOwner && (
            <Link to="/mi-negocio" data-testid="nav-my-business">
              Mi negocio
            </Link>
          )}
          {isOwner && (
            <Link to="/horario" data-testid="nav-hours">
              Horario
            </Link>
          )}
          {isOwner && (
            <Link to="/agenda" data-testid="nav-agenda">
              Agenda
            </Link>
          )}
        </nav>
        {authenticated ? (
          <>
            <span data-testid="current-user">{user?.email}</span>
            <button type="button" onClick={logout} data-testid="logout">
              Salir
            </button>
          </>
        ) : (
          <>
            <Link to="/login">Entrar</Link>
            <Link to="/register">Registro</Link>
          </>
        )}
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
