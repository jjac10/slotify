import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getApiError } from '../services/apiClient'

type AccountType = 'customer' | 'owner'

export function RegisterPage() {
  const { registerCustomer, registerOwner } = useAuth()
  const navigate = useNavigate()
  const [accountType, setAccountType] = useState<AccountType>('customer')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [phone, setPhone] = useState('')
  const [businessName, setBusinessName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      if (accountType === 'owner') {
        await registerOwner({ name, email, password, businessName })
        navigate('/agenda', { replace: true })
      } else {
        await registerCustomer({ name, email, password, phone: phone || undefined })
        navigate('/mis-reservas', { replace: true })
      }
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo completar el registro.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section>
      <h1>Crear cuenta</h1>
      <form onSubmit={handleSubmit}>
        <label>
          Tipo de cuenta
          <select
            data-testid="register-account-type"
            value={accountType}
            onChange={(e) => setAccountType(e.target.value as AccountType)}
          >
            <option value="customer">Cliente</option>
            <option value="owner">Propietario</option>
          </select>
        </label>
        <label>
          Nombre
          <input
            type="text"
            data-testid="register-name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </label>
        <label>
          Email
          <input
            type="email"
            data-testid="register-email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </label>
        <label>
          Contraseña
          <input
            type="password"
            data-testid="register-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        {accountType === 'customer' ? (
          <label>
            Teléfono (opcional)
            <input
              type="tel"
              data-testid="register-phone"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </label>
        ) : (
          <label>
            Nombre del negocio
            <input
              type="text"
              data-testid="register-business-name"
              value={businessName}
              onChange={(e) => setBusinessName(e.target.value)}
              required
            />
          </label>
        )}
        {error && (
          <p role="alert" data-testid="register-error">
            {error}
          </p>
        )}
        <button type="submit" data-testid="register-submit" disabled={submitting}>
          {submitting ? 'Creando…' : 'Crear cuenta'}
        </button>
      </form>
      <p>
        ¿Ya tienes cuenta? <Link to="/login">Inicia sesión</Link>
      </p>
    </section>
  )
}
