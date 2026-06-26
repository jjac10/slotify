import { useId } from 'react'

/**
 * Logo Clock & Slot de Slotify: reloj con el minutero en forma de "slot" (pastilla),
 * en degradado morado→cyan. Sigue el mockup de /design/slotify_logo.
 */
export function Logo({ size = 32, withWordmark = true }: { size?: number; withWordmark?: boolean }) {
  // Id único por instancia: si dos logos comparten el id del gradiente, el
  // navegador resuelve mal la referencia y los trazos quedan invisibles.
  const gradId = useId()
  const grad = `url(#${gradId})`
  return (
    <span className="inline-flex items-center gap-2">
      <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id={gradId} x1="6" y1="10" x2="42" y2="38" gradientUnits="userSpaceOnUse">
            <stop stopColor="#7C3AED" />
            <stop offset="1" stopColor="#06B6D4" />
          </linearGradient>
        </defs>
        {/* esfera */}
        <circle cx="24" cy="24" r="18" stroke={grad} strokeWidth="3" />
        {/* marcas de hora */}
        <circle
          cx="24"
          cy="24"
          r="15"
          stroke={grad}
          strokeWidth="2"
          strokeDasharray="1.5 6.35"
          opacity="0.7"
        />
        {/* aguja horaria */}
        <line x1="24" y1="24" x2="15" y2="16" stroke={grad} strokeWidth="3" strokeLinecap="round" />
        {/* minutero = slot (pastilla) */}
        <line x1="24" y1="24" x2="36" y2="21" stroke={grad} strokeWidth="5" strokeLinecap="round" />
        <circle cx="24" cy="24" r="2.4" fill="#fff" stroke={grad} strokeWidth="2" />
      </svg>
      {withWordmark && (
        <span
          className="font-display text-xl font-extrabold tracking-tight"
          style={{
            background: 'linear-gradient(90deg, #7C3AED, #06B6D4)',
            WebkitBackgroundClip: 'text',
            backgroundClip: 'text',
            WebkitTextFillColor: 'transparent',
          }}
        >
          Slotify
        </span>
      )}
    </span>
  )
}
