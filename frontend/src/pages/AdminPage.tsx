import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { adminService, type AdminBusinessResponse } from '../services/adminService'
import { authService } from '../services/authService'
import { getApiError } from '../services/apiClient'

const PAGE_SIZE = 20

/**
 * /admin — moderación de la plataforma (solo el email configurado como admin en el
 * backend; el resto ve "no autorizado" y la API responde 403 igualmente). No da de
 * alta negocios: lista y elimina los de spam/prueba con borrado en cascada.
 */
export function AdminPage() {
  const [allowed, setAllowed] = useState<boolean | null>(null)
  const [q, setQ] = useState('')
  const [searched, setSearched] = useState('')
  const [items, setItems] = useState<AdminBusinessResponse[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [target, setTarget] = useState<AdminBusinessResponse | null>(null)
  const [confirmName, setConfirmName] = useState('')
  const [deleting, setDeleting] = useState(false)

  useEffect(() => {
    authService.me()
      .then((me) => setAllowed(me.isAdmin))
      .catch(() => setAllowed(false))
  }, [])

  const load = useCallback(async (query: string, pageToLoad: number) => {
    setLoading(true)
    setError(null)
    try {
      const data = await adminService.listBusinesses({ q: query || undefined, page: pageToLoad, pageSize: PAGE_SIZE })
      setItems((prev) => (pageToLoad === 1 ? data.items : [...prev, ...data.items]))
      setTotal(data.total)
      setPage(pageToLoad)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo cargar el directorio.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (allowed) load('', 1)
  }, [allowed, load])

  function handleSearch(e: FormEvent) {
    e.preventDefault()
    setSearched(q)
    load(q, 1)
  }

  async function handleDelete() {
    if (!target) return
    setDeleting(true)
    setError(null)
    try {
      await adminService.deleteBusiness(target.id)
      setItems((prev) => prev.filter((b) => b.id !== target.id))
      setTotal((t) => t - 1)
      setTarget(null)
    } catch (err) {
      setError(getApiError(err)?.message ?? 'No se pudo eliminar el negocio.')
    } finally {
      setDeleting(false)
    }
  }

  if (allowed === null) return <p className="text-on-surface-variant">Comprobando permisos…</p>
  if (!allowed) {
    return (
      <section className="mx-auto max-w-md text-center" data-testid="admin-forbidden">
        <span className="material-symbols-outlined text-[48px] text-on-surface-variant/40">lock</span>
        <h1>Zona restringida</h1>
        <p className="text-on-surface-variant">Solo el administrador de la plataforma puede acceder aquí.</p>
      </section>
    )
  }

  return (
    <section>
      <h1>Moderación</h1>
      <p className="mb-stack-md text-on-surface-variant">
        Directorio de negocios de la plataforma. Eliminar un negocio borra todos sus datos
        (servicios, equipo, reservas, reseñas, clientes) de forma irreversible.
      </p>

      <form onSubmit={handleSearch} className="mb-stack-md flex max-w-md gap-stack-sm">
        <input
          type="search"
          className="field-input flex-1"
          placeholder="Buscar por nombre…"
          data-testid="admin-search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <button type="submit" className="btn-secondary" disabled={loading}>Buscar</button>
      </form>

      {error && <p role="alert" className="alert mb-stack-md" data-testid="admin-error">{error}</p>}

      {items.length === 0 && !loading ? (
        <p className="text-on-surface-variant" data-testid="admin-empty">
          {searched ? `Sin resultados para "${searched}".` : 'No hay negocios.'}
        </p>
      ) : (
        <ul className="flex max-w-2xl flex-col gap-stack-sm" data-testid="admin-business-list">
          {items.map((b) => (
            <li key={b.id} className="card flex items-center gap-stack-md" data-testid="admin-business-item">
              <div className="min-w-0 flex-1">
                <p className="truncate font-bold">{b.name}</p>
                <p className="truncate text-sm text-on-surface-variant">
                  {b.ownerEmail} · alta {new Date(b.createdAt).toLocaleDateString('es-ES')}
                </p>
              </div>
              <button
                type="button"
                className="inline-flex items-center gap-1 rounded-xl border border-error/40 px-3 py-1.5 text-sm font-bold text-error transition-colors hover:bg-error-container/20"
                data-testid="admin-delete-open"
                onClick={() => { setTarget(b); setConfirmName('') }}
              >
                <span className="material-symbols-outlined text-[16px]">delete_forever</span>
                Eliminar
              </button>
            </li>
          ))}
        </ul>
      )}

      {items.length < total && (
        <button type="button" className="btn-secondary mt-stack-md" disabled={loading}
          data-testid="admin-load-more" onClick={() => load(searched, page + 1)}>
          {loading ? 'Cargando…' : `Cargar más (${items.length} de ${total})`}
        </button>
      )}

      {target && (
        <div
          className="fixed inset-0 z-50 flex items-end sm:items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
          onClick={(e) => e.target === e.currentTarget && !deleting && setTarget(null)}
        >
          <div className="card w-full max-w-sm flex flex-col gap-stack-md" data-testid="admin-delete-modal">
            <h2 className="text-lg font-bold text-error">Eliminar «{target.name}»</h2>
            <p className="text-sm text-on-surface-variant">
              Se borran todos sus datos, incluidas las reservas futuras. Escribe el nombre exacto
              del negocio para confirmar.
            </p>
            <input
              type="text"
              className="field-input"
              data-testid="admin-delete-name"
              value={confirmName}
              onChange={(e) => setConfirmName(e.target.value)}
              autoFocus
            />
            <div className="flex gap-stack-sm">
              <button
                type="button"
                className="flex-1 rounded-xl bg-error px-stack-md py-3 font-bold text-on-error transition-transform active:scale-95 disabled:opacity-50"
                data-testid="admin-delete-confirm"
                disabled={deleting || confirmName.trim() !== target.name}
                onClick={handleDelete}
              >
                {deleting ? 'Eliminando…' : 'Eliminar definitivamente'}
              </button>
              <button type="button" className="btn-secondary" onClick={() => setTarget(null)} disabled={deleting}>
                Cancelar
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
