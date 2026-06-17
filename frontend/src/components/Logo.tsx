/** Logo Clock & Slot de Slotify: reloj sobre un slot, en degradado morado→cyan. */
export function Logo({ withWordmark = true }: { withWordmark?: boolean }) {
  return (
    <span className="logo">
      <svg width="30" height="30" viewBox="0 0 32 32" fill="none" aria-hidden="true">
        <defs>
          <linearGradient id="slotify-logo" x1="0" y1="0" x2="32" y2="32" gradientUnits="userSpaceOnUse">
            <stop stopColor="#7C3AED" />
            <stop offset="1" stopColor="#06B6D4" />
          </linearGradient>
        </defs>
        <rect x="1" y="1" width="30" height="30" rx="9" fill="url(#slotify-logo)" />
        {/* reloj */}
        <circle cx="16" cy="14" r="6.6" stroke="#fff" strokeWidth="2" fill="none" />
        <path
          d="M16 10.5V14l2.5 1.8"
          stroke="#fff"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {/* slot reservado */}
        <rect x="9.5" y="24" width="13" height="3" rx="1.5" fill="#fff" opacity="0.92" />
      </svg>
      {withWordmark && <span className="logo-word">Slotify</span>}
    </span>
  )
}
