import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  GuestContactInput,
  buildGuestContact,
  isContactValid,
  isValidEmail,
  isValidSpanishPhone,
  type ContactMode,
} from './GuestContactInput'

describe('validación del contacto de invitado', () => {
  it.each(['600111222', '911234567'])('acepta el teléfono español de 9 dígitos %s', (phone) => {
    expect(isValidSpanishPhone(phone)).toBe(true)
  })

  it.each(['60011122', '6001112223', '600 111 22', 'abcdefghi', ''])(
    'rechaza el teléfono inválido "%s"',
    (phone) => {
      expect(isValidSpanishPhone(phone)).toBe(false)
    },
  )

  it('valida emails con formato razonable', () => {
    expect(isValidEmail('ana@example.com')).toBe(true)
    expect(isValidEmail('sin-arroba.com')).toBe(false)
    expect(isValidEmail('a@b')).toBe(false)
  })

  it('isContactValid mira solo el campo del modo activo', () => {
    expect(isContactValid('phone', '600111222', 'email-invalido')).toBe(true)
    expect(isContactValid('email', '123', 'ana@example.com')).toBe(true)
    expect(isContactValid('phone', '123', 'ana@example.com')).toBe(false)
  })

  it('buildGuestContact construye el campo de la API según el modo', () => {
    expect(buildGuestContact('phone', '600 111 222', '')).toEqual({ guestPhone: '+34600111222' })
    expect(buildGuestContact('email', '', ' ana@example.com ')).toEqual({ guestEmail: 'ana@example.com' })
  })
})

/** Arnés controlado mínimo, como lo usan las pantallas de reserva. */
function Harness() {
  const [mode, setMode] = useState<ContactMode>('phone')
  const [phone, setPhone] = useState('')
  const [email, setEmail] = useState('')
  return (
    <GuestContactInput
      mode={mode}
      onModeChange={setMode}
      phoneLocal={phone}
      onPhoneChange={setPhone}
      email={email}
      onEmailChange={setEmail}
    />
  )
}

describe('GuestContactInput', () => {
  it('arranca en modo teléfono (+34) y solo admite 9 dígitos', async () => {
    render(<Harness />)

    const phone = screen.getByTestId('contact-phone')
    expect(phone).toBeInTheDocument()
    expect(screen.queryByTestId('contact-email')).not.toBeInTheDocument()

    await userEvent.type(phone, '600-111-222-99extra')
    // Se descartan los no-dígitos y se corta en 9
    expect(phone).toHaveValue('600111222')
  })

  it('el toggle cambia a Email y de vuelta a Teléfono', async () => {
    render(<Harness />)

    await userEvent.click(screen.getByTestId('contact-mode-email'))
    expect(screen.getByTestId('contact-email')).toBeInTheDocument()
    expect(screen.queryByTestId('contact-phone')).not.toBeInTheDocument()

    await userEvent.click(screen.getByTestId('contact-mode-phone'))
    expect(screen.getByTestId('contact-phone')).toBeInTheDocument()
  })
})
