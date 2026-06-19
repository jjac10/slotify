import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_URL || 'http://localhost:5000'

  return {
    plugins: [react()],
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
