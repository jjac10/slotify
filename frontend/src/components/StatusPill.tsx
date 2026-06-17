/** Etiqueta de estado de una reserva con color semántico (Material 3). */
const MAP: Record<string, { label: string; cls: string }> = {
  pending: { label: 'Pendiente', cls: 'bg-secondary-container/40 text-on-secondary-container' },
  confirmed: { label: 'Confirmada', cls: 'bg-primary-container/15 text-primary' },
  cancelled: { label: 'Cancelada', cls: 'bg-error-container text-on-error-container' },
  'no-show': { label: 'No-show', cls: 'bg-surface-container-high text-on-surface-variant' },
}

export function StatusPill({ status }: { status: string }) {
  const s = MAP[status] ?? { label: status, cls: 'bg-surface-container-high text-on-surface-variant' }
  return <span className={`pill ${s.cls}`}>{s.label}</span>
}
