import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// Tests unitarios de componentes (Vitest + React Testing Library, jsdom).
// Los e2e de Playwright viven en tests/e2e y NO los ejecuta Vitest.
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    include: ['src/**/*.test.{ts,tsx}'],
    css: false,
  },
})
