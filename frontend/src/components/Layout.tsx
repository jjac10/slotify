import { useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { Logo } from './Logo'

interface NavItem {
  to: string
  label: string
  icon: string
  testid?: string
}

export function Layout() {
  const { user, status, isOwner, isStaff, logout } = useAuth()
  const authenticated = status === 'authenticated'
  const [menuOpen, setMenuOpen] = useState(false)
  const initial = user?.email?.[0]?.toUpperCase() ?? 'U'

  // Re-clic en el nav de la ruta actual → remonta la página (recarga sus datos).
  const location = useLocation()
  const [reloadKey, setReloadKey] = useState(0)
  const handleNavClick = (to: string) => {
    if (location.pathname === to) setReloadKey((k) => k + 1)
  }

  const items: NavItem[] = [
    { to: '/explorar', label: 'Explorar', icon: 'explore' },
    { to: '/mis-reservas', label: 'Reservas', icon: 'event', testid: 'nav-my-reservations' },
    // Cliente puro (ni owner ni empleado): sus reseñas.
    ...(authenticated && !isOwner && !isStaff
      ? [{ to: '/mis-resenas', label: 'Mis reseñas', icon: 'reviews', testid: 'nav-my-reviews' }]
      : []),
    // Panel (solo owner) va antes de la Agenda.
    ...(isOwner
      ? [{ to: '/panel', label: 'Panel', icon: 'dashboard', testid: 'nav-dashboard' }]
      : []),
    // Agenda: el owner y los empleados (estos ven solo sus reservas).
    ...(isOwner || isStaff
      ? [{ to: '/agenda', label: 'Agenda', icon: 'calendar_today', testid: 'nav-agenda' }]
      : []),
    // Configuración (solo owner) al final.
    ...(isOwner
      ? [{ to: '/configuracion', label: 'Configuración', icon: 'settings', testid: 'nav-settings' }]
      : []),
  ]

  return (
    <div className="min-h-screen bg-surface-dim/30">
      {/* Sidebar (escritorio) — los data-testid de navegación viven aquí */}
      <aside className="hidden md:flex fixed inset-y-0 left-0 z-40 w-56 flex-col border-r border-outline-variant/50 bg-surface px-3 py-stack-md">
        <Link to="/" className="px-2 py-1" aria-label="Slotify — inicio">
          <Logo />
        </Link>
        <nav className="mt-stack-lg flex flex-1 flex-col gap-1">
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              data-testid={it.testid}
              onClick={() => handleNavClick(it.to)}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors ${
                  isActive ? 'bg-primary-container text-on-primary' : 'text-on-surface-variant hover:bg-surface-container-low'
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
        <a
          href="https://github.com/jjac10/slotify/releases"
          target="_blank"
          rel="noreferrer"
          className="mt-stack-sm px-3 py-1 text-[11px] font-medium text-on-surface-variant/60 hover:text-on-surface-variant"
        >
          Slotify v{__APP_VERSION__}
        </a>
      </aside>

      {/* Top bar (logo en móvil + perfil arriba a la derecha) */}
      <header className="sticky top-0 z-30 flex h-16 items-center px-container-mobile md:pl-[calc(14rem+1rem)] md:pr-stack-lg bg-surface/85 backdrop-blur">
        <Link to="/" className="md:hidden" aria-label="Slotify — inicio">
          <Logo />
        </Link>
        <div className="relative ml-auto">
          {authenticated ? (
            <>
              <button
                type="button"
                data-testid="profile-button"
                onClick={() => setMenuOpen((o) => !o)}
                className="flex items-center gap-2 rounded-full py-1 pl-1 pr-2 transition-colors hover:bg-surface-container-low"
              >
                <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary-container text-sm font-bold text-on-primary">
                  {initial}
                </span>
                <span data-testid="current-user" className="hidden max-w-[12rem] truncate text-sm text-on-surface-variant sm:inline">
                  {user?.email}
                </span>
                <span className="material-symbols-outlined text-[20px] text-on-surface-variant">expand_more</span>
              </button>
              {menuOpen && (
                <>
                  <div className="fixed inset-0 z-30" onClick={() => setMenuOpen(false)} aria-hidden />
                  <div className="absolute right-0 z-40 mt-2 w-52 overflow-hidden rounded-xl border border-outline-variant/50 bg-surface-container-lowest p-1 shadow-lift">
                    <Link
                      to={isOwner ? '/panel' : '/inicio'}
                      onClick={() => setMenuOpen(false)}
                      className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-on-surface hover:bg-surface-container-low"
                    >
                      <span className="material-symbols-outlined text-[20px]">home</span>
                      Inicio
                    </Link>
                    <button
                      type="button"
                      data-testid="logout"
                      onClick={logout}
                      className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-on-surface hover:bg-surface-container-low"
                    >
                      <span className="material-symbols-outlined text-[20px]">logout</span>
                      Salir
                    </button>
                  </div>
                </>
              )}
            </>
          ) : (
            <div className="flex items-center gap-stack-sm">
              <NavLink to="/login" className="rounded-full px-3.5 py-1.5 text-sm font-semibold text-on-surface-variant hover:bg-surface-container-low">
                Entrar
              </NavLink>
              <Link to="/register" className="btn-primary py-2 text-sm">
                Registro
              </Link>
            </div>
          )}
        </div>
      </header>

      {/* Contenido */}
      <main className="md:pl-56">
        <div className="mx-auto max-w-md md:max-w-3xl px-container-mobile md:px-container-desktop py-stack-lg pb-28 md:pb-stack-xl">
          {/* key cambia al re-pulsar el nav de la ruta actual → remonta y recarga */}
          <Outlet key={reloadKey} />
        </div>
      </main>

      {/* Bottom nav (móvil, sin data-testid) */}
      {items.length > 0 && (
        <nav className="md:hidden fixed bottom-0 left-1/2 z-30 w-full max-w-md -translate-x-1/2 border-t border-outline-variant/50 bg-surface/95 backdrop-blur shadow-[0_-2px_12px_rgba(17,28,45,0.06)]">
          <div className="flex h-16 items-stretch justify-around overflow-x-auto hide-scrollbar">
            {items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
                onClick={() => handleNavClick(it.to)}
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
