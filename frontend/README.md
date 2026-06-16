# Slotify Frontend

React 19 + TypeScript + Vite PWA para Slotify.

## Stack
- **Framework:** React 19
- **Language:** TypeScript 5.x
- **Build:** Vite
- **Styling:** TBD (CSS Modules / Tailwind)
- **Testing:** Vitest + React Testing Library + Playwright

## Estructura
```
frontend/
├── src/
│   ├── components/
│   ├── pages/
│   ├── hooks/
│   ├── services/
│   ├── types/
│   └── App.tsx
├── tests/
├── vite.config.ts
└── tsconfig.json
```

## Ejecutar Localmente
```bash
cd frontend
npm install
npm run dev
```

App estará en `http://localhost:5173`

## Testing
```bash
npm run test          # Unit tests
npm run test:e2e      # E2E con Playwright
```

## Build
```bash
npm run build
