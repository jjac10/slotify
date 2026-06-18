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
    { to: '/explorar', label: 'Explorar', icon: 'explore' },
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
    <div className="min-h-screen bg-surface-dim/30">
      {/* Sidebar (escritorio) — aquí viven los data-testid (Playwright corre a 1280px) */}
      <aside className="hidden md:flex fixed inset-y-0 left-0 z-30 w-56 flex-col border-r border-outline-variant/50 bg-surface px-3 py-stack-md">
        <Link to="/" className="px-2 py-1" aria-label="Slotify — inicio">
          <Logo />
        </Link>
        <nav className="mt-stack-lg flex flex-1 flex-col gap-1">
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              data-testid={it.testid}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors ${
                  isActive
                    ? 'bg-primary-container text-on-primary'
                    : 'text-on-surface-variant hover:bg-surface-container-low'
                }`
              }
            >
              {({ isActive }) => (
                <>
                  <span className={`material-symbols-outlined text-[22px] ${isActive ? 'fill' : ''}`}>{it.icon}</span>
                  {it.label}
                </>
              )}
            </NavLink>
          ))}
        </nav>
        {authenticated ? (
          <div className="border-t border-outline-variant/50 pt-stack-sm">
            <p data-testid="current-user" className="truncate px-2 text-xs text-on-surface-variant">{user?.email}</p>
            <button
              type="button"
              onClick={logout}
              data-testid="logout"
              className="mt-1 flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold text-on-surface-variant transition-colors hover:bg-surface-container-low"
            >
              <span className="material-symbols-outlined text-[22px]">logout</span>
              Salir
            </button>
          </div>
        ) : (
          <div className="flex flex-col gap-stack-sm">
            <NavLink to="/login" className="rounded-xl px-3 py-2.5 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low">
              Entrar
            </NavLink>
            <Link to="/register" className="btn-primary text-sm">
              Registro
            </Link>
          </div>
        )}
      </aside>

      {/* Top bar (móvil) */}
      <header className="md:hidden sticky top-0 z-30 flex h-16 items-center px-container-mobile bg-surface/90 backdrop-blur">
        <Link to="/" aria-label="Slotify — inicio">
          <Logo />
        </Link>
        <div className="ml-auto">
          {authenticated ? (
            <button
              type="button"
              onClick={logout}
              aria-label="Salir"
              className="flex h-9 w-9 items-center justify-center rounded-full text-on-surface-variant hover:bg-surface-container-low active:scale-95"
            >
              <span className="material-symbols-outlined text-[20px]">logout</span>
            </button>
          ) : (
            <Link to="/login" className="rounded-full bg-primary-container px-stack-md py-2 text-sm font-bold text-on-primary">
              Entrar
            </Link>
          )}
        </div>
      </header>

      {/* Contenido */}
      <main className="md:pl-56">
        <div className="mx-auto max-w-md md:max-w-3xl px-container-mobile md:px-container-desktop py-stack-lg pb-28 md:pb-stack-xl">
          <Outlet />
        </div>
      </main>

      {/* Bottom nav (móvil, sin data-testid para no duplicar) */}
      {items.length > 0 && (
        <nav className="md:hidden fixed bottom-0 left-1/2 z-30 w-full max-w-md -translate-x-1/2 border-t border-outline-variant/50 bg-surface/95 backdrop-blur shadow-[0_-2px_12px_rgba(17,28,45,0.06)]">
          <div className="flex h-16 items-stretch justify-around overflow-x-auto hide-scrollbar">
            {items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
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
  )
}
