/**
 * Estrellas de valoración. `RatingStars` es solo lectura (tarjetas, ficha);
 * `StarInput` es interactivo (formulario de reseña). Usa el icono `star` de
 * Material Symbols con la clase `fill` para la estrella rellena.
 */

interface RatingStarsProps {
  /** Media 0–5; null = sin reseñas. */
  value: number | null
  /** Nº de reseñas, para mostrar "(12)". */
  count?: number
  /** Tamaño del icono en px. */
  size?: number
  testId?: string
}

/** Muestra 5 estrellas (rellenas según la media) + nota numérica y, opcional, el recuento. */
export function RatingStars({ value, count, size = 16, testId }: RatingStarsProps) {
  if (value == null) {
    return (
      <span className="inline-flex items-center gap-1 text-xs text-on-surface-variant" data-testid={testId}>
        <span className="material-symbols-outlined text-on-surface-variant/40" style={{ fontSize: size }}>star</span>
        Sin reseñas
      </span>
    )
  }
  const rounded = Math.round(value)
  return (
    <span className="inline-flex items-center gap-1" data-testid={testId} aria-label={`${value.toFixed(1)} de 5`}>
      <span className="inline-flex">
        {[1, 2, 3, 4, 5].map((i) => (
          <span
            key={i}
            className={`material-symbols-outlined ${i <= rounded ? 'fill text-amber-500' : 'text-on-surface-variant/30'}`}
            style={{ fontSize: size }}
          >
            star
          </span>
        ))}
      </span>
      <span className="text-xs font-bold text-on-surface">{value.toFixed(1)}</span>
      {count != null && count > 0 && <span className="text-xs text-on-surface-variant">({count})</span>}
    </span>
  )
}

interface StarInputProps {
  value: number
  onChange: (rating: number) => void
  testId?: string
}

/** Selector interactivo de 1–5 estrellas. */
export function StarInput({ value, onChange, testId }: StarInputProps) {
  return (
    <div className="inline-flex gap-1" role="radiogroup" aria-label="Valoración" data-testid={testId}>
      {[1, 2, 3, 4, 5].map((i) => (
        <button
          key={i}
          type="button"
          role="radio"
          aria-checked={value === i}
          aria-label={`${i} estrella${i > 1 ? 's' : ''}`}
          data-testid={`${testId ?? 'star'}-${i}`}
          onClick={() => onChange(i)}
          className="rounded p-0.5 transition-transform hover:scale-110 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        >
          <span className={`material-symbols-outlined text-[32px] ${i <= value ? 'fill text-amber-500' : 'text-on-surface-variant/40'}`}>
            star
          </span>
        </button>
      ))}
    </div>
  )
}
