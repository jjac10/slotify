import { useState } from 'react'
import type { FormEvent } from 'react'
import { businessService } from '../services/businessService'
import { getApiError } from '../services/apiClient'
import { tokenStorage } from '../services/tokenStorage'

/**
 * "Zona de peligro" del negocio: eliminarlo con TODOS sus datos (cascada, RGPD).
 * Confirmación máxima: hay que escribir el nombre exacto del negocio Y la contraseña
 * actual. Bloqueado por el backend si hay reservas futuras activas (409). Tras el
 * borrado, la cuenta sigue viva pero sin negocio: recarga completa hacia la home.
 */
export function DeleteBusinessSection({ businessId, businessName }: { businessId: string; businessName: string }) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [password, setPassword] = useState('')
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function openModal() {
    setName('')
    setPassword('')
    setError(null)
    setOpen(true)
  }

  async function handleConfirm(e: FormEvent) {
    e.preventDefault()
    setError(null)
    setDeleting(true)
    try {
      await businessService.deleteBusiness(businessId, name, password)
      // La sesión sigue viva (la cuenta no se toca), pero ya no hay negocio.
      tokenStorage.clearBusiness()
      window.location.assign('/')
    } catch (err) {
      const apiErr = getApiError(err)
      setError(
        apiErr?.error === 'name_mismatch'
          ? 'El nombre no coincide exactamente con el del negocio.'
          : apiErr?.error === 'business_has_future_reservations'
            ? 'Hay reservas futuras activas: cancélalas (o espera a que pasen) antes de eliminar el negocio.'
            : apiErr?.message ?? 'No se pudo eliminar el negocio.',
      )
      setDeleting(false)
    }
  }

  return (
    <div className="flex flex-col gap-stack-sm" data-testid="delete-business-zone">
      <p className="text-sm text-on-surface-variant">
        Eliminar el negocio borra de forma permanente sus servicios, equipo, horarios, reservas,
        reseñas y clientes. Tu cuenta personal no se elimina. Esta acción no se puede deshacer.
      </p>
      <button
        type="button"
        onClick={openModal}
        data-testid="delete-business-open"
        className="self-start inline-flex items-center gap-1.5 rounded-xl border border-error/40 px-stack-md py-2 text-sm font-bold text-error transition-colors hover:bg-error-container/20"
      >
        <span className="material-symbols-outlined text-[18px]">storefront</span>
        Eliminar el negocio
      </button>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
          onClick={(e) => e.target === e.currentTarget && !deleting && setOpen(false)}
        >
          <form onSubmit={handleConfirm} className="card w-full max-w-sm flex flex-col gap-stack-md" data-testid="delete-business-modal">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-bold text-error">Eliminar el negocio</h2>
              <button type="button" onClick={() => setOpen(false)} className="p-1 rounded-lg hover:bg-surface-container-low" aria-label="Cerrar" data-testid="delete-business-cancel">
                <span className="material-symbols-outlined text-[22px] text-on-surface-variant">close</span>
              </button>
            </div>

            <div className="rounded-xl border border-error/30 bg-error-container/15 px-stack-md py-3 text-sm">
              <p className="font-semibold">Esta acción es irreversible.</p>
              <ul className="mt-1 list-disc pl-4 text-on-surface-variant text-xs flex flex-col gap-0.5">
                <li>Se borran servicios, equipo, horarios, festivos, reservas, reseñas y clientes.</li>
                <li>Si hay reservas futuras de clientes, cancélalas primero.</li>
                <li>Tu cuenta personal seguirá existiendo, sin negocio.</li>
              </ul>
            </div>

            <div className="field">
              <label className="field-label" htmlFor="delete-business-name">
                Escribe el nombre exacto del negocio: <span className="font-bold">{businessName}</span>
              </label>
              <input
                id="delete-business-name"
                type="text"
                className="field-input"
                data-testid="delete-business-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoComplete="off"
                required
              />
            </div>

            <div className="field">
              <label className="field-label" htmlFor="delete-business-password">
                Confirma con tu contraseña
              </label>
              <input
                id="delete-business-password"
                type="password"
                className="field-input"
                data-testid="delete-business-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </div>

            {error && <p role="alert" className="alert text-sm" data-testid="delete-business-error">{error}</p>}

            <button
              type="submit"
              data-testid="delete-business-confirm"
              disabled={deleting || name.trim() !== businessName || password.length === 0}
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
