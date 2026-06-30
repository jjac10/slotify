import { readFileSync } from 'node:fs'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// Versión de la app = la de package.json (fuente única). Se expone como __APP_VERSION__.
const pkg = JSON.parse(readFileSync(new URL('./package.json', import.meta.url), 'utf-8')) as { version: string }

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_URL || 'http://localhost:5000'

  return {
    define: {
      __APP_VERSION__: JSON.stringify(pkg.version),
    },
    plugins: [
      react(),
      // PWA: instalable + cacheo de la shell para arranque offline. Las llamadas a /api
      // no se cachean (NetworkOnly) para no servir datos obsoletos.
      VitePWA({
        registerType: 'autoUpdate',
        includeAssets: ['favicon.svg'],
        manifest: {
          name: 'Slotify — Reservas',
          short_name: 'Slotify',
          description: 'Reserva en tu negocio local en segundos.',
          lang: 'es',
          theme_color: '#630ed4',
          background_color: '#ffffff',
          display: 'standalone',
          start_url: '/',
          icons: [
            { src: 'favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
          ],
        },
        workbox: {
          navigateFallbackDenylist: [/^\/api/, /^\/scalar/],
          runtimeCaching: [
            { urlPattern: /\/api\//, handler: 'NetworkOnly' },
          ],
        },
      }),
    ],
    server: {
      port: process.env.PORT ? parseInt(process.env.PORT) : 5173,
      strictPort: !process.env.PORT,
      // El navegador llama a /api/... (mismo origen) y Vite lo reenvía al backend,
      // replicando lo que nginx hace en producción (infra/nginx.conf). Sin CORS en dev.
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          rewrite: (path) => path.replace(/^\/api/, ''),
        },
      },
    },
  }
})
