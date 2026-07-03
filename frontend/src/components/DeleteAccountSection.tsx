import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../hooks/useAuth'
import { getApiError } from '../services/apiClient'

/**
 * "Zona de peligro": eliminar la cuenta (derecho de supresión RGPD, prometido en
 * la página de privacidad). Abre un modal que exige la contraseña actual y avisa
 * de que la operación es irreversible; para un owner, además, de que su negocio
 * se elimina por completo. Tras el borrado, useAuth cierra sesión y redirige a
 * la landing con un mensaje.
 */
export function DeleteAccountSection() {
  const { deleteAccount, isOwner } = useAuth()
  const [open, setOpen] = useState(false)
  const [password, setPassword] = useState('')
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function openModal() {
    setPassword('')
    setError(null)
    setOpen(true)
  }

  async function handleConfirm(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setDeleting(true)
    try {
      await deleteAccount(password) // si va bien, navega fuera: no hace falta cerrar el modal
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo eliminar la cuenta.')
      setDeleting(false)
    }
  }

  return (
    <div className="flex flex-col gap-stack-sm" data-testid="danger-zone">
      <p className="text-sm text-on-surface-variant">
        Eliminar tu cuenta borra tus datos personales de forma permanente
        {isOwner ? ', incluido tu negocio con sus servicios, equipo y reservas' : ''}. Esta acción no se
        puede deshacer.
      </p>
      <button
        type="button"
        onClick={openModal}
        data-testid="delete-account-open"
        className="self-start inline-flex items-center gap-1.5 rounded-xl border border-error/40 px-stack-md py-2 text-sm font-bold text-error transition-colors hover:bg-error-container/20"
      >
        <span className="material-symbols-outlined text-[18px]">delete_forever</span>
        Eliminar mi cuenta
      </button>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
          onClick={(e) => e.target === e.currentTarget && !deleting && setOpen(false)}
        >
          <form onSubmit={handleConfirm} className="card w-full max-w-sm flex flex-col gap-stack-md" data-testid="delete-account-modal">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-error">Eliminar mi cuenta</h2>
              <button type="button" onClick={() => setOpen(false)} className="p-1 rounded-lg hover:bg-surface-container-low" aria-label="Cerrar" data-testid="delete-account-cancel">
                <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
              </button>
            </div>

            <div className="rounded-xl border border-error/30 bg-error-container/15 px-stack-md py-3 text-sm">
              <p className="font-semibold">Esta acción es irreversible.</p>
              <ul className="mt-1 list-disc pl-4 text-on-surface-variant text-xs flex flex-col gap-0.5">
                {isOwner ? (
                  <>
                    <li>Tu negocio se eliminará por completo: servicios, equipo, horarios y reservas.</li>
                    <li>Si tienes reservas futuras de clientes, cancélalas primero.</li>
                  </>
                ) : (
                  <>
                    <li>Tus reservas futuras se cancelarán.</li>
                    <li>Tus datos personales se eliminarán; el historial de los negocios queda anonimizado.</li>
                  </>
                )}
                <li>No podrás volver a iniciar sesión con esta cuenta.</li>
              </ul>
            </div>

            <div className="field">
              <label className="field-label" htmlFor="delete-account-password">
                Confirma con tu contraseña
              </label>
              <input
                id="delete-account-password"
                type="password"
                className="field-input"
                data-testid="delete-account-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </div>

            {error && <p role="alert" className="alert text-sm" data-testid="delete-account-error">{error}</p>}

            <button
              type="submit"
              data-testid="delete-account-confirm"
              disabled={deleting || password.length === 0}
              className="inline-flex items-center justify-center gap-1.5 rounded-xl bg-error px-stack-md py-3 font-bold text-on-error transition-transform active:scale-95 disabled:opacity-50"
            >
              <span className="material-symbols-outlined text-[18px]">delete_forever</span>
              {deleting ? 'Eliminando…' : 'Eliminar definitivamente'}
            </button>
          </form>
        </div>
      )}
    </div>
  )
}
