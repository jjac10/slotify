export type ContactMode = 'phone' | 'email'

/** Teléfono español válido: 9 dígitos (el prefijo +34 lo aporta el selector de país). */
export function isValidSpanishPhone(localDigits: string): boolean {
  return /^\d{9}$/.test(localDigits.trim())
}

export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())
}

/** ¿El contacto del modo activo es válido? */
export function isContactValid(mode: ContactMode, phoneLocal: string, email: string): boolean {
  return mode === 'phone' ? isValidSpanishPhone(phoneLocal) : isValidEmail(email)
}

/** Construye los campos de invitado para la API según el modo activo. */
export function buildGuestContact(mode: ContactMode, phoneLocal: string, email: string): { guestPhone?: string; guestEmail?: string } {
  return mode === 'phone'
    ? { guestPhone: `+34${phoneLocal.replace(/\D/g, '')}` }
    : { guestEmail: email.trim() }
}

interface Props {
  mode: ContactMode
  onModeChange: (m: ContactMode) => void
  phoneLocal: string
  onPhoneChange: (v: string) => void
  email: string
  onEmailChange: (v: string) => void
  testidPrefix?: string
}

/**
 * Campo de contacto del cliente para las pantallas de reserva: alterna Teléfono
 * (con selector de país 🇪🇸 +34, solo España de momento; solo dígitos) o Email.
 */
export function GuestContactInput({
  mode, onModeChange, phoneLocal, onPhoneChange, email, onEmailChange, testidPrefix = 'contact',
}: Props) {
  return (
    <div className="field">
      <label className="field-label">Contacto del cliente</label>
      <div className="inline-flex rounded-full border border-outline-variant/50 bg-surface-container p-1 gap-1 mb-2 self-start">
        {(['phone', 'email'] as const).map((m) => (
          <button key={m} type="button" onClick={() => onModeChange(m)}
            data-testid={`${testidPrefix}-mode-${m}`}
            className={`rounded-full px-3 py-1 text-xs font-bold transition-colors ${
              mode === m ? 'bg-primary text-on-primary' : 'text-on-surface-variant'
            }`}>
            {m === 'phone' ? 'Teléfono' : 'Email'}
          </button>
        ))}
      </div>
      {mode === 'phone' ? (
        <div className="flex gap-2">
          <select className="field-input w-28 shrink-0" disabled aria-label="País" data-testid={`${testidPrefix}-country`}>
            <option value="ES">🇪🇸 +34</option>
          </select>
          <input type="tel" inputMode="numeric" className="field-input flex-1" data-testid={`${testidPrefix}-phone`}
            value={phoneLocal} onChange={(e) => onPhoneChange(e.target.value.replace(/\D/g, '').slice(0, 9))}
            placeholder="600 000 000" />
        </div>
      ) : (
        <input type="email" className="field-input" data-testid={`${testidPrefix}-email`}
          value={email} onChange={(e) => onEmailChange(e.target.value)} placeholder="cliente@email.com" />
      )}
    </div>
  )
}
