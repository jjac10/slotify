/** Categorías de negocio (mismos códigos que el backend; etiqueta para mostrar). */
export const BUSINESS_CATEGORIES: ReadonlyArray<{ code: string; label: string; icon: string }> = [
  { code: 'peluqueria', label: 'Peluquería', icon: 'content_cut' },
  { code: 'barberia', label: 'Barbería', icon: 'cut' },
  { code: 'estetica', label: 'Estética', icon: 'spa' },
  { code: 'unas', label: 'Uñas', icon: 'back_hand' },
  { code: 'spa', label: 'Spa & Masajes', icon: 'self_improvement' },
  { code: 'depilacion', label: 'Depilación', icon: 'waving_hand' },
  { code: 'maquillaje', label: 'Maquillaje', icon: 'brush' },
  { code: 'tatuajes', label: 'Tatuajes', icon: 'gesture' },
  { code: 'fisioterapia', label: 'Fisioterapia', icon: 'healing' },
  { code: 'otros', label: 'Otros', icon: 'storefront' },
]

export function categoryLabel(code: string | null): string | null {
  if (!code) return null
  return BUSINESS_CATEGORIES.find((c) => c.code === code)?.label ?? code
}

export function categoryIcon(code: string | null): string {
  if (!code) return 'storefront'
  return BUSINESS_CATEGORIES.find((c) => c.code === code)?.icon ?? 'storefront'
}
