/** Etiqueta de estado de una reserva con color semántico. */
const MAP: Record<string, { label: string; cls: string; icon: string }> = {
  pending: { label: 'Pendiente', cls: 'bg-amber-100 text-amber-700', icon: 'schedule' },
  confirmed: { label: 'Confirmada', cls: 'bg-emerald-100 text-emerald-700', icon: 'check_circle' },
  cancelled: { label: 'Cancelada', cls: 'bg-error-container text-on-error-container', icon: 'cancel' },
  'no-show': { label: 'No-show', cls: 'bg-surface-container-high text-on-surface-variant', icon: 'person_off' },
}

export function StatusPill({ status }: { status: string }) {
  const s = MAP[status] ?? { label: status, cls: 'bg-surface-container-high text-on-surface-variant', icon: 'help' }
  return (
    <span className={`pill gap-1 ${s.cls}`}>
      <span className="material-symbols-outlined text-[14px]">{s.icon}</span>
      {s.label}
    </span>
  )
}
