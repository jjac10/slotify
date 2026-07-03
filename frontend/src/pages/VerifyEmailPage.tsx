import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { authService } from '../services/authService'
import { getApiError } from '../services/apiClient'
import { Logo } from '../components/Logo'
import { useAuth } from '../hooks/useAuth'

type VerifyState = 'verifying' | 'success' | 'error'

/**
 * /verificar-email?token=... — el usuario llega desde el enlace del email (simulado).
 * Página pública: verifica el token automáticamente y muestra éxito o error. El token
 * es de un solo uso, por eso el guard con ref evita el doble POST del StrictMode.
 */
export function VerifyEmailPage() {
  const [searchParams] = useSearchParams()
  const token = searchParams.get('token') ?? ''
  const { status } = useAuth()
  const [state, setState] = useState<VerifyState>(token ? 'verifying' : 'error')
  const [message, setMessage] = useState<string | null>(null)
  const requested = useRef(false)

  useEffect(() => {
    if (!token || requested.current) return
    requested.current = true // un solo POST: el token es de un solo uso
    authService
      .verifyEmail({ token })
      .then(() => setState('success'))
      .catch((err) => {
        setMessage(getApiError(err)?.message ?? null)
        setState('error')
      })
  }, [token])

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Verificación de email</h1>
      </div>

      <div className="card text-center">
        {state === 'verifying' && (
          <p className="text-sm text-on-surface-variant" data-testid="verify-email-pending">
            Verificando tu email…
          </p>
        )}

        {state === 'success' && (
          <div className="flex flex-col items-center gap-stack-md" data-testid="verify-email-success">
            <span className="material-symbols-outlined text-[40px] text-primary" aria-hidden>
              mark_email_read
            </span>
            <p className="font-semibold text-on-surface">¡Email verificado! Gracias.</p>
            <Link
              to={status === 'authenticated' ? '/' : '/login'}
              className="btn-primary"
              data-testid="verify-email-continue"
            >
              {status === 'authenticated' ? 'Ir a mi inicio' : 'Iniciar sesión'}
            </Link>
          </div>
        )}

        {state === 'error' && (
          <div className="flex flex-col gap-stack-md" data-testid="verify-email-error">
            <p role="alert" className="alert">
              {message ?? 'El enlace de verificación no es válido o ha caducado.'}
            </p>
            <p className="text-sm text-on-surface-variant">
              Si ya tienes cuenta, entra y usa «Reenviar enlace» en el aviso de tu inicio
              para recibir uno nuevo.
            </p>
            <Link to="/login" className="font-semibold text-primary hover:underline">
              Ir a iniciar sesión
            </Link>
          </div>
        )}
      </div>
    </section>
  )
}
