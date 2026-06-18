import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getApiError } from '../services/apiClient'
import { Logo } from '../components/Logo'

type AccountType = 'customer' | 'owner'

export function RegisterPage() {
  const { registerCustomer, registerOwner } = useAuth()
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [accountType, setAccountType] = useState<AccountType>(
    searchParams.get('type') === 'owner' ? 'owner' : 'customer',
  )
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [phone, setPhone] = useState('')
  const [businessName, setBusinessName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)
  // Si llegas con intención (?type=owner|customer) no mostramos el selector.
  const explicitType = searchParams.get('type') === 'owner' || searchParams.get('type') === 'customer'

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      if (accountType === 'owner') {
        await registerOwner({ name, email, password, businessName })
      } else {
        await registerCustomer({ name, email, password, phone: phone || undefined })
      }
      navigate('/', { replace: true })
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo completar el registro.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <section className="mx-auto max-w-md">
      <div className="flex flex-col items-center text-center mb-stack-lg">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">{accountType === 'owner' ? 'Crea tu cuenta de propietario' : 'Crear cuenta'}</h1>
        <p className="text-on-surface-variant">
          {accountType === 'owner' ? 'Registra tu negocio en Slotify.' : 'Reserva en segundos.'}
        </p>
      </div>

      <div className="card">
        <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md">
          {!explicitType && (
            <div className="field">
              <label className="field-label" htmlFor="register-account-type">Tipo de cuenta</label>
              <select
                id="register-account-type"
                data-testid="register-account-type"
                value={accountType}
                onChange={(e) => setAccountType(e.target.value as AccountType)}
                className="field-input"
              >
                <option value="customer">Cliente</option>
                <option value="owner">Propietario</option>
              </select>
            </div>
          )}

          <div className="field">
            <label className="field-label" htmlFor="register-name">Nombre</label>
            <input id="register-name" type="text" className="field-input" data-testid="register-name"
              value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="register-email">Email</label>
            <input id="register-email" type="email" className="field-input" data-testid="register-email"
              value={email} onChange={(e) => setEmail(e.target.value)} required />
          </div>
          <div className="field">
            <label className="field-label" htmlFor="register-password">Contraseña</label>
            <input id="register-password" type="password" className="field-input" data-testid="register-password"
              value={password} onChange={(e) => setPassword(e.target.value)} required />
          </div>

          {accountType === 'customer' ? (
            <div className="field">
              <label className="field-label" htmlFor="register-phone">Teléfono (opcional)</label>
              <input id="register-phone" type="tel" className="field-input" data-testid="register-phone"
                value={phone} onChange={(e) => setPhone(e.target.value)} />
            </div>
          ) : (
            <div className="field">
              <label className="field-label" htmlFor="register-business-name">Nombre del negocio</label>
              <input id="register-business-name" type="text" className="field-input" data-testid="register-business-name"
                value={businessName} onChange={(e) => setBusinessName(e.target.value)} required />
            </div>
          )}

          {error && (
            <p role="alert" className="alert" data-testid="register-error">
              {error}
            </p>
          )}
          <button type="submit" className="btn-primary w-full" data-testid="register-submit" disabled={submitting}>
            {submitting ? 'Creando…' : 'Crear cuenta'}
          </button>
        </form>
      </div>

      <p className="mt-stack-md text-center text-sm text-on-surface-variant">
        ¿Ya tienes cuenta?{' '}
        <Link to="/login" className="font-semibold text-primary hover:underline">
          Inicia sesión
        </Link>
      </p>
    </section>
  )
}
