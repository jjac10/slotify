import { Link, NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { Logo } from './Logo'

interface NavItem {
  to: string
  label: string
  icon: string
  testid?: string
}

export function Layout() {
  const { user, status, isOwner, logout } = useAuth()
  const authenticated = status === 'authenticated'

  const items: NavItem[] = [
    { to: '/reservar', label: 'Reservar', icon: 'event_available' },
    ...(authenticated
      ? [{ to: '/mis-reservas', label: 'Mis reservas', icon: 'event', testid: 'nav-my-reservations' }]
      : []),
    ...(isOwner
      ? [
          { to: '/panel', label: 'Panel', icon: 'dashboard', testid: 'nav-dashboard' },
          { to: '/mi-negocio', label: 'Mi negocio', icon: 'storefront', testid: 'nav-my-business' },
          { to: '/horario', label: 'Horario', icon: 'schedule', testid: 'nav-hours' },
          { to: '/agenda', label: 'Agenda', icon: 'calendar_today', testid: 'nav-agenda' },
        ]
      : []),
  ]

  return (
    <div className="min-h-screen bg-background text-on-surface">
      {/* Top app bar */}
      <header className="sticky top-0 z-40 flex items-center gap-stack-md h-16 px-container-mobile md:px-container-desktop bg-surface/85 backdrop-blur border-b border-outline-variant/50">
        <Link to="/" aria-label="Slotify — inicio" className="shrink-0">
          <Logo />
        </Link>

        {/* Desktop nav (los data-testid viven aquí) */}
        <nav className="hidden md:flex flex-1 items-center gap-1 overflow-x-auto hide-scrollbar">
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              data-testid={it.testid}
              className={({ isActive }) =>
                `whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm font-semibold transition-colors ${
                  isActive
                    ? 'bg-primary-container text-on-primary'
                    : 'text-on-surface-variant hover:bg-surface-container-low'
                }`
              }
            >
              {it.label}
            </NavLink>
          ))}
        </nav>

        <div className="flex flex-1 md:flex-none items-center justify-end gap-stack-sm">
          {authenticated ? (
            <>
              <span data-testid="current-user" className="hidden sm:inline text-sm text-on-surface-variant max-w-[14rem] truncate">
                {user?.email}
              </span>
              <button type="button" onClick={logout} data-testid="logout" className="btn-secondary py-2 text-sm">
                Salir
              </button>
            </>
          ) : (
            <>
              <NavLink to="/login" className="rounded-full px-3.5 py-1.5 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low">
                Entrar
              </NavLink>
              <Link to="/register" className="btn-primary py-2 text-sm">
                Registro
              </Link>
            </>
          )}
        </div>
      </header>

      {/* Contenido (con hueco inferior para la bottom-nav en móvil) */}
      <main className="mx-auto max-w-3xl px-container-mobile md:px-container-desktop py-stack-lg pb-28 md:pb-stack-xl">
        <Outlet />
      </main>

      {/* Bottom nav (solo móvil; sin data-testid para no duplicar) */}
      {items.length > 0 && (
        <nav className="md:hidden fixed bottom-0 inset-x-0 z-40 flex justify-around items-stretch h-16 bg-surface border-t border-outline-variant/60 shadow-[0_-2px_10px_rgba(0,0,0,0.05)]">
          {items.slice(0, 5).map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              className={({ isActive }) =>
                `flex flex-col items-center justify-center gap-0.5 flex-1 transition-opacity active:scale-95 ${
                  isActive ? 'text-primary' : 'text-on-surface-variant'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  <span className={`material-symbols-outlined text-[22px] ${isActive ? 'fill' : ''}`}>{it.icon}</span>
                  <span className="text-[11px] font-medium">{it.label}</span>
                </>
              )}
            </NavLink>
          ))}
        </nav>
      )}
    </div>
  )
}
