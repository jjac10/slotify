import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'
// Matchers de jest-dom (toBeInTheDocument, toBeDisabled, …) sobre expect de Vitest.
import '@testing-library/jest-dom/vitest'

// Sin globals de Vitest: la limpieza automática de RTL se registra a mano.
afterEach(cleanup)
