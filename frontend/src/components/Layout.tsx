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
    { to: '/reservar', label: 'Reservar', icon: 'add_circle' },
    ...(authenticated
      ? [{ to: '/mis-reservas', label: 'Reservas', icon: 'event', testid: 'nav-my-reservations' }]
      : []),
    ...(isOwner
      ? [
          { to: '/panel', label: 'Panel', icon: 'dashboard', testid: 'nav-dashboard' },
          { to: '/mi-negocio', label: 'Negocio', icon: 'storefront', testid: 'nav-my-business' },
          { to: '/horario', label: 'Horario', icon: 'schedule', testid: 'nav-hours' },
          { to: '/agenda', label: 'Agenda', icon: 'calendar_today', testid: 'nav-agenda' },
        ]
      : []),
  ]

  return (
    <div className="min-h-screen bg-surface-dim/40">
      {/* Marco tipo app, centrado */}
      <div className="relative mx-auto flex min-h-screen max-w-md flex-col bg-background shadow-xl">
        {/* Top app bar */}
        <header className="sticky top-0 z-30 flex h-16 items-center gap-stack-sm px-container-mobile bg-surface/90 backdrop-blur">
          <Link to="/" aria-label="Slotify — inicio" className="shrink-0">
            <Logo />
          </Link>
          <div className="ml-auto flex items-center gap-stack-sm">
            {authenticated ? (
              <>
                <span data-testid="current-user" className="hidden xs:inline max-w-[8.5rem] truncate text-xs text-on-surface-variant">
                  {user?.email}
                </span>
                <button
                  type="button"
                  onClick={logout}
                  data-testid="logout"
                  aria-label="Salir"
                  className="flex h-9 w-9 items-center justify-center rounded-full text-on-surface-variant transition-colors hover:bg-surface-container-low active:scale-95"
                >
                  <span className="material-symbols-outlined text-[20px]">logout</span>
                </button>
              </>
            ) : (
              <>
                <NavLink to="/login" className="rounded-full px-3 py-1.5 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low">
                  Entrar
                </NavLink>
                <Link to="/register" className="btn-primary px-stack-md py-2 text-sm">
                  Registro
                </Link>
              </>
            )}
          </div>
        </header>

        {/* Contenido */}
        <main className="flex-1 px-container-mobile py-stack-lg pb-28">
          <Outlet />
        </main>

        {/* Bottom nav */}
        {items.length > 0 && (
          <nav className="fixed bottom-0 left-1/2 z-30 w-full max-w-md -translate-x-1/2 border-t border-outline-variant/50 bg-surface/95 backdrop-blur shadow-[0_-2px_12px_rgba(17,28,45,0.06)]">
            <div className="flex h-16 items-stretch justify-around overflow-x-auto hide-scrollbar">
              {items.map((it) => (
                <NavLink
                  key={it.to}
                  to={it.to}
                  data-testid={it.testid}
                  className={({ isActive }) =>
                    `flex min-w-[4rem] flex-col items-center justify-center gap-0.5 px-1 transition-colors active:scale-95 ${
                      isActive ? 'text-primary' : 'text-on-surface-variant'
                    }`
                  }
                >
                  {({ isActive }) => (
                    <>
                      <span className={`material-symbols-outlined text-[24px] ${isActive ? 'fill' : ''}`}>{it.icon}</span>
                      <span className="text-[11px] font-medium">{it.label}</span>
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          </nav>
        )}
      </div>
    </div>
  )
}
