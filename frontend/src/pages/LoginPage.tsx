import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getApiError } from '../services/apiClient'

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
    <section className="auth-shell">
      <h1>Iniciar sesión</h1>
      <p className="muted">Accede a tu cuenta de Slotify.</p>
      <div className="card">
        <form onSubmit={handleSubmit}>
          <label>
            Email
            <input
              type="email"
              data-testid="login-email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </label>
          <label>
            Contraseña
            <input
              type="password"
              data-testid="login-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
            />
          </label>
          {error && (
            <p role="alert" data-testid="login-error">
              {error}
            </p>
          )}
          <button type="submit" data-testid="login-submit" disabled={submitting}>
            {submitting ? 'Entrando…' : 'Entrar'}
          </button>
        </form>
      </div>
      <p style={{ marginTop: '1rem' }}>
        ¿No tienes cuenta? <Link to="/register">Regístrate</Link>
      </p>
    </section>
  )
}
