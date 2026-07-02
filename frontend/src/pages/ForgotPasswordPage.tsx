import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { authService } from '../services/authService'
import { Logo } from '../components/Logo'

/**
 * /recuperar — pide el email y muestra SIEMPRE el mismo mensaje genérico de éxito
 * (exista o no la cuenta), igual que el backend: nada de enumerar usuarios.
 */
export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    try {
      await authService.forgotPassword({ email })
    } catch {
      // Mismo mensaje genérico también si algo falla: no revelamos nada.
    } finally {
      setSent(true)
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Recuperar contraseña</h1>
        <p className="text-on-surface-variant">
          Te enviaremos un enlace para restablecerla.
        </p>
      </div>

      <div className="card">
        {sent ? (
          <div className="flex flex-col gap-stack-md text-center" data-testid="forgot-success">
            <p className="rounded-xl border border-primary-container/20 bg-surface-container-high px-4 py-3 text-sm font-medium text-primary">
              Si el email existe, recibirás instrucciones para restablecer tu contraseña.
            </p>
            <p className="text-sm text-on-surface-variant">
              Revisa tu bandeja de entrada (y la carpeta de spam).
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md">
            <div className="field">
              <label className="field-label" htmlFor="forgot-email">Email</label>
              <input
                id="forgot-email"
                type="email"
                className="field-input"
                data-testid="forgot-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                autoFocus
              />
            </div>
            <button type="submit" className="btn-primary w-full" data-testid="forgot-submit" disabled={submitting}>
              {submitting ? 'Enviando…' : 'Enviar instrucciones'}
            </button>
          </form>
        )}
      </div>

      <p className="mt-stack-md text-center text-sm text-on-surface-variant">
        ¿La recuerdas?{' '}
        <Link to="/login" className="font-semibold text-primary hover:underline">
          Inicia sesión
        </Link>
      </p>
    </section>
  )
}
