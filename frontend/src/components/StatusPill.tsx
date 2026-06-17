/** Etiqueta de estado de una reserva con color semántico. */
const LABELS: Record<string, string> = {
  pending: 'Pendiente',
  confirmed: 'Confirmada',
  cancelled: 'Cancelada',
  'no-show': 'No-show',
}

const CLASSES: Record<string, string> = {
  pending: 'pill-pending',
  confirmed: 'pill-confirmed',
  cancelled: 'pill-cancelled',
}

export function StatusPill({ status }: { status: string }) {
  return <span className={`pill ${CLASSES[status] ?? ''}`}>{LABELS[status] ?? status}</span>
}
