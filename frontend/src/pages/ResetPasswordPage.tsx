import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { authService } from '../services/authService'
import { getApiError } from '../services/apiClient'
import { Logo } from '../components/Logo'

/**
 * /restablecer?token=... — el usuario llega desde el enlace del email, fija su nueva
 * contraseña (x2) y vuelve a login con un mensaje de éxito. Token inválido/caducado
 * → mensaje claro + enlace para pedir otro.
 */
export function ResetPasswordPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const token = searchParams.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [tokenRejected, setTokenRejected] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    if (password !== confirm) {
      setError('Las contraseñas no coinciden.')
      return
    }
    setSubmitting(true)
    try {
      await authService.resetPassword({ token, newPassword: password })
      navigate('/login', {
        replace: true,
        state: { message: 'Contraseña actualizada. Ya puedes iniciar sesión.' },
      })
    } catch (err) {
      const apiError = getApiError(err)
      if (apiError?.error === 'invalid_reset_token') {
        setTokenRejected(true)
      } else {
        setError(apiError?.message ?? 'No se pudo restablecer la contraseña. Inténtalo de nuevo.')
      }
      setSubmitting(false)
    }
  }

  const invalidToken = !token || tokenRejected

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Nueva contraseña</h1>
      </div>

      <div className="card">
        {invalidToken ? (
          <div className="flex flex-col gap-stack-md text-center" data-testid="reset-invalid-token">
            <p role="alert" className="alert">
              El enlace de recuperación no es válido o ha caducado.
            </p>
            <Link to="/recuperar" className="font-semibold text-primary hover:underline" data-testid="reset-request-again">
              Solicitar un enlace nuevo
            </Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md" data-testid="reset-form">
            <p className="text-sm text-on-surface-variant">
              Elige una contraseña nueva para tu cuenta (mínimo 8 caracteres, con
              mayúscula, minúscula, dígito y símbolo).
            </p>
            <div className="field">
              <label className="field-label" htmlFor="reset-password">Nueva contraseña</label>
              <input
                id="reset-password"
                type="password"
                className="field-input"
                data-testid="reset-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                minLength={8}
                autoFocus
              />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="reset-confirm">Repite la contraseña</label>
              <input
                id="reset-confirm"
                type="password"
                className="field-input"
                data-testid="reset-confirm"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                required
                minLength={8}
              />
            </div>
            {error && (
              <p role="alert" className="alert" data-testid="reset-error">
                {error}
              </p>
            )}
            <button type="submit" className="btn-primary w-full" data-testid="reset-submit" disabled={submitting}>
              {submitting ? 'Guardando…' : 'Cambiar contraseña'}
            </button>
          </form>
        )}
      </div>
    </section>
  )
}
