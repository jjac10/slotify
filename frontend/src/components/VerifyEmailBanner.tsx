import { useState } from 'react'
import { useAuth } from '../hooks/useAuth'
import { authService } from '../services/authService'

/**
 * Aviso discreto y descartable: la cuenta aún no verificó su email. NO bloquea nada
 * (la verificación es opcional); permite reenviar el enlace o descartar el aviso.
 * Vive en el flujo normal del layout (no es fixed): nunca tapa otros controles.
 */
export function VerifyEmailBanner() {
  const { user, status } = useAuth()
  const dismissKey = user ? `slotify.verifyEmailBannerDismissed.${user.userId}` : ''
  const [dismissed, setDismissed] = useState(false)
  const [sendState, setSendState] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle')

  if (status !== 'authenticated' || !user || user.emailVerified) return null
  if (dismissed || (dismissKey && sessionStorage.getItem(dismissKey) === '1')) return null

  async function handleResend() {
    setSendState('sending')
    try {
      await authService.resendVerification()
      setSendState('sent')
    } catch {
      setSendState('error')
    }
  }

  function handleDismiss() {
    if (dismissKey) sessionStorage.setItem(dismissKey, '1')
    setDismissed(true)
  }

  return (
    <div
      data-testid="verify-email-banner"
      className="mb-stack-md flex flex-wrap items-center gap-x-3 gap-y-1 rounded-xl border border-outline-variant/60 bg-surface-container-low px-4 py-2.5 text-sm text-on-surface-variant"
    >
      <span className="material-symbols-outlined text-[20px] text-primary" aria-hidden>
        mark_email_unread
      </span>
      {sendState === 'sent' ? (
        <span data-testid="verify-email-sent" className="flex-1">
          Enlace reenviado. Revisa tu bandeja de entrada.
        </span>
      ) : (
        <>
          <span className="flex-1">
            <strong className="font-semibold text-on-surface">Verifica tu email</strong> — te
            enviamos un enlace a {user.email}.
          </span>
          <button
            type="button"
            data-testid="verify-email-resend"
            onClick={handleResend}
            disabled={sendState === 'sending'}
            className="font-semibold text-primary hover:underline disabled:opacity-60"
          >
            {sendState === 'sending' ? 'Enviando…' : 'Reenviar enlace'}
          </button>
          {sendState === 'error' && (
            <span role="alert" className="text-error">
              No se pudo reenviar. Inténtalo más tarde.
            </span>
          )}
        </>
      )}
      <button
        type="button"
        data-testid="verify-email-dismiss"
        onClick={handleDismiss}
        aria-label="Descartar aviso"
        className="ml-auto flex h-6 w-6 items-center justify-center rounded-full text-on-surface-variant hover:bg-surface-container"
      >
        <span className="material-symbols-outlined text-[18px]" aria-hidden>
          close
        </span>
      </button>
    </div>
  )
}
