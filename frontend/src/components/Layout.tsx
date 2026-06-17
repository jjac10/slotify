import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { Logo } from './Logo'

export function Layout() {
  const { user, status, isOwner, logout } = useAuth()
  const authenticated = status === 'authenticated'

  return (
    <div>
      <header className="app-header">
        <Link to="/" className="brand-link" aria-label="Slotify — inicio">
          <Logo />
        </Link>
        <nav className="app-nav">
          <NavLink to="/reservar">Reservar</NavLink>
          {authenticated && (
            <NavLink to="/mis-reservas" data-testid="nav-my-reservations">
              Mis reservas
            </NavLink>
          )}
          {isOwner && (
            <NavLink to="/panel" data-testid="nav-dashboard">
              Panel
            </NavLink>
          )}
          {isOwner && (
            <NavLink to="/mi-negocio" data-testid="nav-my-business">
              Mi negocio
            </NavLink>
          )}
          {isOwner && (
            <NavLink to="/horario" data-testid="nav-hours">
              Horario
            </NavLink>
          )}
          {isOwner && (
            <NavLink to="/agenda" data-testid="nav-agenda">
              Agenda
            </NavLink>
          )}
        </nav>
        {authenticated ? (
          <div className="app-user">
            <span data-testid="current-user">{user?.email}</span>
            <button type="button" className="btn-secondary" onClick={logout} data-testid="logout">
              Salir
            </button>
          </div>
        ) : (
          <div className="app-user">
            <NavLink to="/login">Entrar</NavLink>
            <Link to="/register" className="btn" style={{ textDecoration: 'none' }}>
              Registro
            </Link>
          </div>
        )}
      </header>
      <main>
        <Outlet />
      </main>
    </div>
  )
}
