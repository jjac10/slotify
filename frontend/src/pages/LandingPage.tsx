import { Link, useLocation } from 'react-router-dom'
import { Logo } from '../components/Logo'

const FEATURES = [
  {
    icon: 'no_accounts',
    title: 'Reserva sin registro',
    text: 'Entra, elige tu servicio y confirma. Sin formularios largos ni descargas innecesarias.',
  },
  {
    icon: 'notifications_active',
    title: 'Recordatorios automáticos',
    text: 'Te avisamos para que nunca se te olvide tu cita.',
  },
  {
    icon: 'person_pin',
    title: 'Hiper-personalización',
    text: 'El sistema aprende tus preferencias para ofrecerte los mejores huecos.',
  },
]

const NAV_LINKS = [
  { label: 'Inicio', href: '#inicio' },
  { label: 'Ventajas', href: '#ventajas' },
  { label: 'Móvil', href: '#movil' },
]

export function LandingPage() {
  // Mensaje de despedida tras eliminar la cuenta (useAuth navega aquí con este state).
  const accountDeleted = Boolean((useLocation().state as { accountDeleted?: boolean } | null)?.accountDeleted)

  return (
    <div className="min-h-screen bg-background">
      {accountDeleted && (
        <div
          className="flex items-center justify-center gap-2 bg-secondary-container px-4 py-3 text-sm font-semibold text-on-secondary-container"
          data-testid="account-deleted-banner"
          role="status"
        >
          <span className="material-symbols-outlined text-[18px]">check_circle</span>
          Tu cuenta se ha eliminado. Sentimos verte marchar: aquí tienes tu casa si vuelves.
        </div>
      )}

      {/* Header fijo blanco */}
      <header className="sticky top-0 z-50 bg-white shadow-sm">
        <nav
          aria-label="Principal"
          className="mx-auto flex h-16 max-w-7xl items-center justify-between px-container-mobile md:px-container-desktop"
        >
          <Link to="/" className="flex items-center gap-2">
            <Logo />
          </Link>
          <div className="flex items-center gap-stack-lg">
            {NAV_LINKS.map((l) => (
              <a
                key={l.href}
                href={l.href}
                className="hidden text-sm font-medium text-on-surface-variant transition-colors hover:text-primary md:block"
              >
                {l.label}
              </a>
            ))}
            <Link
              to="/login"
              className="rounded-full bg-primary px-6 py-2.5 text-sm font-semibold text-on-primary transition-all hover:brightness-110 active:scale-95"
            >
              Acceder
            </Link>
          </div>
        </nav>
      </header>

      <main>
        {/* Hero */}
        <section
          id="inicio"
          className="hero-gradient relative flex scroll-mt-20 flex-col items-center justify-center overflow-hidden px-container-mobile py-stack-xl text-center text-white md:min-h-[85vh] md:py-16"
        >
          {/* Elementos decorativos */}
          <div aria-hidden className="absolute -right-24 -top-24 h-96 w-96 rounded-full bg-secondary-container opacity-20 blur-[100px]" />
          <div aria-hidden className="absolute -bottom-24 -left-24 h-96 w-96 rounded-full bg-primary-container opacity-30 blur-[100px]" />

          <div className="relative z-10 mx-auto max-w-4xl">
            <span className="mb-stack-md inline-block rounded-full border border-white/20 bg-white/10 px-4 py-1.5 text-xs font-semibold backdrop-blur-sm">
              Simple • Rápido • Sin Instalación
            </span>
            <h1 className="mb-stack-md font-display text-4xl font-extrabold leading-tight tracking-tight md:text-5xl">
              Reserva tu cita <br className="hidden md:block" />
              <span className="text-secondary-fixed">sin descargas</span>
            </h1>
            <p className="mx-auto mb-stack-xl max-w-2xl text-white/80">
              La plataforma más intuitiva para reservar servicios locales. Olvídate de registros pesados y
              aplicaciones que ocupan espacio.
            </p>

            {/* CTAs */}
            <div className="flex flex-col items-center justify-center gap-stack-md md:flex-row">
              <Link
                to="/explorar"
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-secondary-container px-8 py-4 font-bold text-on-secondary-container shadow-lg shadow-secondary/20 transition-all hover:shadow-xl active:scale-95 md:w-auto"
              >
                <span className="material-symbols-outlined" aria-hidden>
                  search
                </span>
                Buscar negocio
              </Link>
              <Link
                to="/register?type=owner"
                className="flex w-full items-center justify-center gap-2 rounded-xl border-2 border-white bg-transparent px-8 py-4 font-bold text-white transition-all hover:bg-white/10 active:scale-95 md:w-auto"
              >
                <span className="material-symbols-outlined" aria-hidden>
                  storefront
                </span>
                Soy propietario
              </Link>
            </div>
          </div>

          {/* Tarjeta flotante ilustrativa */}
          <div aria-hidden className="animate-float relative z-10 mx-auto mt-stack-xl w-full max-w-sm">
            <div className="rounded-2xl border border-white/20 bg-white/10 p-6 shadow-2xl backdrop-blur-md">
              <div className="mb-4 flex items-center justify-between">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 rounded-full bg-secondary-fixed" />
                  <div className="text-left">
                    <p className="text-xs font-bold text-white">Barbería Elite</p>
                    <p className="text-[10px] text-white/60">Disponible hoy</p>
                  </div>
                </div>
                <span className="material-symbols-outlined text-secondary-fixed">verified</span>
              </div>
              <div className="mb-4 grid grid-cols-3 gap-2">
                <div className="h-8 rounded-lg bg-white/20" />
                <div className="flex h-8 items-center justify-center rounded-lg bg-secondary-fixed text-[10px] font-bold text-on-secondary-container">
                  16:30
                </div>
                <div className="h-8 rounded-lg bg-white/20" />
              </div>
              <div className="h-10 w-full rounded-lg bg-white" />
            </div>
          </div>
        </section>

        {/* Features */}
        <section id="ventajas" className="mx-auto max-w-7xl scroll-mt-20 px-container-mobile py-stack-xl md:px-container-desktop">
          <div className="grid grid-cols-1 gap-stack-lg md:grid-cols-3">
            {FEATURES.map((f) => (
              <article
                key={f.title}
                className="group rounded-2xl border border-outline-variant bg-white p-stack-md transition-all hover:border-primary"
              >
                <div className="mb-stack-md flex h-12 w-12 items-center justify-center rounded-xl bg-surface-container-high text-primary transition-colors group-hover:bg-primary group-hover:text-white">
                  <span className="material-symbols-outlined" aria-hidden>
                    {f.icon}
                  </span>
                </div>
                <h2 className="mb-2 !text-lg text-on-surface">{f.title}</h2>
                <p className="text-sm text-on-surface-variant">{f.text}</p>
              </article>
            ))}
          </div>
        </section>

        {/* Accede desde tu móvil */}
        <section id="movil" className="scroll-mt-20 bg-surface-container-low px-container-mobile py-stack-xl md:px-container-desktop">
          <div className="mx-auto flex max-w-5xl flex-col items-center gap-stack-xl rounded-3xl border border-outline-variant bg-white p-stack-xl shadow-sm md:flex-row">
            <div className="flex-1 text-center md:text-left">
              <h2 className="mb-stack-md text-on-surface">Accede desde tu móvil</h2>
              <p className="mb-stack-lg text-sm text-on-surface-variant">
                Escanea el código QR para descubrir los mejores negocios cerca de ti. Sin descargar nada,
                directamente en tu navegador móvil con la mejor experiencia de usuario.
              </p>
              <ul className="flex flex-wrap justify-center gap-4 md:justify-start">
                <li className="flex items-center gap-2 text-xs font-medium text-on-surface-variant">
                  <span className="material-symbols-outlined fill text-primary" aria-hidden>
                    check_circle
                  </span>
                  Optimizado para iOS &amp; Android
                </li>
                <li className="flex items-center gap-2 text-xs font-medium text-on-surface-variant">
                  <span className="material-symbols-outlined fill text-primary" aria-hidden>
                    check_circle
                  </span>
                  Carga ultra-rápida
                </li>
              </ul>
            </div>
            <div
              aria-hidden
              className="group relative flex h-48 w-48 shrink-0 items-center justify-center overflow-hidden rounded-2xl border-4 border-white bg-surface-container shadow-lg"
            >
              <div className="absolute inset-0 bg-gradient-to-br from-primary/5 to-secondary/5" />
              <span className="material-symbols-outlined text-8xl text-primary/40 transition-transform duration-300 group-hover:scale-110">
                qr_code_2
              </span>
              <div className="absolute right-2 top-2 h-2 w-2 animate-pulse rounded-full bg-primary" />
            </div>
          </div>
        </section>

        {/* Franja CTA de cierre */}
        <section className="mx-auto max-w-7xl px-container-mobile py-stack-xl md:px-container-desktop">
          <div className="hero-gradient relative overflow-hidden rounded-3xl px-stack-lg py-stack-xl text-center text-white">
            <div aria-hidden className="absolute -right-16 -top-16 h-64 w-64 rounded-full bg-secondary-container opacity-20 blur-[80px]" />
            <h2 className="relative z-10 text-white">¿Tienes un negocio?</h2>
            <p className="relative z-10 mx-auto mt-stack-sm max-w-xl text-white/80">
              Publica tus servicios, gestiona tu agenda y recibe reservas online en minutos. Gratis para
              empezar.
            </p>
            <Link
              to="/register?type=owner"
              className="relative z-10 mt-stack-lg inline-flex items-center justify-center gap-2 rounded-xl bg-white px-8 py-3.5 font-bold text-primary transition-all hover:shadow-xl active:scale-95"
            >
              <span className="material-symbols-outlined" aria-hidden>
                rocket_launch
              </span>
              Crear mi negocio
            </Link>
          </div>
        </section>
      </main>

      <footer className="border-t border-outline-variant/30 bg-surface-container-highest/30 px-container-mobile py-stack-xl md:px-container-desktop">
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-stack-md md:flex-row">
          <div className="flex items-center gap-2">
            <Logo withWordmark={false} size={24} />
            <span className="text-xs font-bold uppercase tracking-wider text-on-surface-variant">Slotify</span>
          </div>
          <nav aria-label="Legal" className="flex flex-wrap justify-center gap-stack-md text-sm text-on-surface-variant">
            <Link to="/legal/terminos" className="hover:text-primary hover:underline">
              Términos
            </Link>
            <Link to="/legal/privacidad" className="hover:text-primary hover:underline">
              Privacidad
            </Link>
            <Link to="/legal/cookies" className="hover:text-primary hover:underline">
              Cookies
            </Link>
            <Link to="/contacto" className="hover:text-primary hover:underline">
              Contacto
            </Link>
          </nav>
          <p className="text-xs text-on-surface-variant/70">© 2026 Slotify · TFM</p>
        </div>
      </footer>
    </div>
  )
}
