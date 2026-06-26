import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { authService } from '../services/authService'
import { getApiError } from '../services/apiClient'
import { Logo } from '../components/Logo'
import type { StaffInviteInfoResponse } from '../types/api'

/**
 * Pantalla a la que llega el empleado con su enlace de invitación (/invitacion/:token):
 * ve a qué negocio se une, fija su contraseña y queda logueado como trabajador.
 */
export function AcceptInvitePage() {
  const { token = '' } = useParams()
  const navigate = useNavigate()
  const { acceptStaffInvite } = useAuth()
  const [invite, setInvite] = useState<StaffInviteInfoResponse | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  useEffect(() => {
    let active = true
    authService.getStaffInvite(token)
      .then((data) => { if (active) setInvite(data) })
      .catch((err) => { if (active) setLoadError(getApiError(err)?.message ?? 'La invitación no es válida o ya se usó.') })
    return () => { active = false }
  }, [token])

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!invite) return
    setError(null)
    setSubmitting(true)
    try {
      await acceptStaffInvite(token, password, invite.email)
      navigate('/agenda', { replace: true })
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo crear la cuenta.')
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Únete al equipo</h1>
      </div>

      <div className="card">
        {loadError ? (
          <p role="alert" className="alert" data-testid="invite-error">{loadError}</p>
        ) : !invite ? (
          <p className="text-on-surface-variant">Cargando invitación…</p>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md" data-testid="accept-invite-form">
            <p className="text-sm text-on-surface-variant">
              <span className="font-semibold text-on-surface">{invite.staffName}</span>, te unes a{' '}
              <span className="font-semibold text-on-surface">{invite.businessName}</span>. Crea tu contraseña para acceder a tu agenda.
            </p>
            <div className="field">
              <label className="field-label" htmlFor="invite-email">Email</label>
              <input id="invite-email" type="email" className="field-input" value={invite.email} disabled data-testid="invite-email" />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="invite-password">Contraseña</label>
              <input id="invite-password" type="password" className="field-input" data-testid="invite-password"
                value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} autoFocus />
            </div>
            {error && <p role="alert" className="alert" data-testid="accept-error">{error}</p>}
            <button type="submit" className="btn-primary w-full" data-testid="accept-submit" disabled={submitting}>
              {submitting ? 'Creando…' : 'Crear cuenta y entrar'}
            </button>
          </form>
        )}
      </div>
    </section>
  )
}
