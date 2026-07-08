import { useState } from 'react'
import type { FormEvent } from 'react'
import axios from 'axios'
import { Logo } from '../components/Logo'
import { supportService } from '../services/supportService'

/**
 * /contacto — formulario público de soporte/sugerencias. El backend "envía"
 * el mensaje al dueño de la plataforma (simulado en el TFM, swappable por
 * proveedor real) y responde 204; aquí solo confirmamos el envío.
 */
export function ContactPage() {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [sent, setSent] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [errors, setErrors] = useState<string[]>([])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitting(true)
    setErrors([])
    try {
      await supportService.sendContactMessage({ name, email, message })
      setSent(true)
    } catch (error) {
      if (axios.isAxiosError(error) && error.response) {
        const data = error.response.data as { message?: string; details?: string[] }
        setErrors(
          Array.isArray(data.details) && data.details.length > 0
            ? data.details
            : [data.message ?? 'No se pudo enviar el mensaje. Inténtalo de nuevo.'],
        )
      } else {
        setErrors(['No se pudo enviar el mensaje. Inténtalo de nuevo.'])
      }
    } finally {
      setSubmitting(false)
    }
  }

  function resetForm() {
    setSent(false)
    setName('')
    setEmail('')
    setMessage('')
    setErrors([])
  }

  return (
    <section className="mx-auto max-w-md">
      <div className="mb-stack-lg flex flex-col items-center text-center">
        <Logo withWordmark={false} size={48} />
        <h1 className="mt-stack-sm">Contacto y soporte</h1>
        <p className="text-on-surface-variant">
          ¿Dudas, sugerencias o algo que no funciona? Escríbenos.
        </p>
      </div>

      <div className="card">
        {sent ? (
          <div className="flex flex-col gap-stack-md text-center" data-testid="contact-success">
            <p className="rounded-xl border border-primary-container/20 bg-surface-container-high px-4 py-3 text-sm font-medium text-primary">
              Mensaje enviado. Te responderemos lo antes posible en el email indicado.
            </p>
            <button type="button" className="btn-secondary" onClick={resetForm}>
              Enviar otro mensaje
            </button>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col gap-stack-md" noValidate>
            <div className="field">
              <label className="field-label" htmlFor="contact-name">Nombre</label>
              <input
                id="contact-name"
                type="text"
                className="field-input"
                data-testid="contact-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                maxLength={100}
                required
                autoFocus
              />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="contact-email">Email</label>
              <input
                id="contact-email"
                type="email"
                className="field-input"
                data-testid="contact-email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                maxLength={254}
                required
              />
            </div>
            <div className="field">
              <label className="field-label" htmlFor="contact-message">Mensaje</label>
              <textarea
                id="contact-message"
                className="field-input min-h-32 resize-y"
                data-testid="contact-message"
                value={message}
                onChange={(e) => setMessage(e.target.value)}
                maxLength={2000}
                required
              />
            </div>

            {errors.length > 0 && (
              <ul className="flex flex-col gap-1 rounded-xl bg-error-container/40 px-4 py-3 text-sm text-on-error-container" data-testid="contact-error" role="alert">
                {errors.map((error) => (
                  <li key={error}>{error}</li>
                ))}
              </ul>
            )}

            <button
              type="submit"
              className="btn-primary w-full"
              data-testid="contact-submit"
              disabled={submitting || !name.trim() || !email.trim() || !message.trim()}
            >
              {submitting ? 'Enviando…' : 'Enviar mensaje'}
            </button>
          </form>
        )}
      </div>
    </section>
  )
}
