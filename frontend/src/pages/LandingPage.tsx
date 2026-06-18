import { Link } from 'react-router-dom'
import { Logo } from '../components/Logo'

const FEATURES = [
  {
    icon: 'bolt',
    title: 'Reserva sin registro',
    text: 'Entra, elige tu servicio y confirma. Sin formularios largos ni descargas innecesarias.',
  },
  {
    icon: 'notifications_active',
    title: 'Recordatorios automáticos',
    text: 'Te avisamos para que nunca se te olvide tu cita.',
  },
  {
    icon: 'tune',
    title: 'Hiper-personalización',
    text: 'El sistema aprende tus preferencias para ofrecerte los mejores huecos.',
  },
]

export function LandingPage() {
  return (
    <div className="min-h-screen bg-background">
      {/* Hero */}
      <header className="relative overflow-hidden bg-gradient-to-br from-primary via-primary-container to-secondary text-on-primary">
        <div className="mx-auto max-w-5xl px-container-mobile md:px-container-desktop">
          <nav className="flex h-16 items-center justify-between">
            <Link to="/" className="flex items-center gap-2">
              <Logo />
            </Link>
            <Link to="/login" className="rounded-full bg-white/15 px-stack-md py-2 text-sm font-bold backdrop-blur transition-colors hover:bg-white/25">
              Acceder
            </Link>
          </nav>

          <div className="py-stack-xl md:py-16 md:max-w-2xl">
            <span className="inline-flex items-center rounded-full bg-white/15 px-3 py-1 text-xs font-semibold backdrop-blur">
              Simple · Rápido · Sin registro
            </span>
            <h1 className="mt-stack-md font-display text-4xl font-extrabold leading-tight tracking-tight md:text-5xl">
              Reserva tu cita sin descargas
            </h1>
            <p className="mt-stack-md max-w-xl text-white/85">
              La plataforma más intuitiva para reservar servicios locales. Olvídate de registros pesados y
              aplicaciones que ocupan espacio.
            </p>
            <div className="mt-stack-lg flex flex-col gap-stack-sm sm:flex-row">
              <Link
                to="/explorar"
                className="inline-flex items-center justify-center gap-2 rounded-xl bg-secondary-container px-stack-lg py-3 font-bold text-on-secondary-container shadow-card transition-transform active:scale-95"
              >
                <span className="material-symbols-outlined">search</span>
                Buscar negocio
              </Link>
              <Link
                to="/register?type=owner"
                className="inline-flex items-center justify-center gap-2 rounded-xl border border-white/40 px-stack-lg py-3 font-bold text-on-primary transition-colors hover:bg-white/10"
              >
                <span className="material-symbols-outlined">storefront</span>
                Soy propietario
              </Link>
            </div>
          </div>
        </div>
      </header>

      {/* Features */}
      <section className="mx-auto max-w-5xl px-container-mobile md:px-container-desktop py-stack-xl">
        <div className="grid gap-stack-md md:grid-cols-3">
          {FEATURES.map((f) => (
            <article key={f.title} className="card">
              <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-container/15 text-primary">
                <span className="material-symbols-outlined">{f.icon}</span>
              </span>
              <h2 className="!mt-stack-md !text-lg">{f.title}</h2>
              <p className="text-sm text-on-surface-variant">{f.text}</p>
            </article>
          ))}
        </div>

        <div className="mt-stack-xl flex flex-col items-center gap-stack-sm text-center">
          <h2>¿List@ para reservar?</h2>
          <p className="text-on-surface-variant">Encuentra tu negocio y reserva en segundos.</p>
          <Link to="/explorar" className="btn-primary mt-stack-sm">
            Explorar negocios
          </Link>
        </div>
      </section>

      <footer className="border-t border-outline-variant/50 py-stack-lg text-center text-sm text-on-surface-variant">
        <div className="flex items-center justify-center gap-2">
          <Logo withWordmark={false} size={20} />
          <span className="font-display font-bold">Slotify</span>
        </div>
        <p className="mt-stack-sm">Términos · Privacidad · Contacto</p>
        <p className="mt-1 text-xs">© 2026 Slotify · TFM</p>
      </footer>
    </div>
  )
}
