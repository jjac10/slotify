import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getApiError } from '../services/apiClient'
import { Logo } from '../components/Logo'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login({ email, password })
      const from = (location.state as { from?: string } | null)?.from
      navigate(from ?? '/mis-reservas', { replace: true })
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo iniciar sesión. Revisa tus credenciales.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Iniciar sesión</h1>
        <p className="text-on-surface-variant">Accede a tu cuenta de Slotify.</p>
      </div>

      <div className="card">
        <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md">
          <div className="field">
            <label className="field-label" htmlFor="login-email">Email</label>
            <input
              id="login-email"
              type="email"
              className="field-input"
              data-testid="login-email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="login-password">Contraseña</label>
            <input
              id="login-password"
              type="password"
              className="field-input"
              data-testid="login-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </div>
          {error && (
            <p role="alert" className="alert" data-testid="login-error">
              {error}
            </p>
          )}
          <button type="submit" className="btn-primary w-full" data-testid="login-submit" disabled={submitting}>
            {submitting ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
      </div>

      <p className="mt-stack-md text-center text-sm text-on-surface-variant">
        ¿No tienes cuenta?{' '}
        <Link to="/register" className="font-semibold text-primary hover:underline">
          Regístrate
        </Link>
      </p>
    </section>
  )
}
