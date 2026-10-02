/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    // Playwright owns everything under e2e/ (its own *.spec.ts convention);
    // Vitest's default include glob would otherwise try to execute those
    // files too and fail immediately on test.describe() outside a Playwright run.
    exclude: ['e2e/**', 'node_modules/**'],
  },
})
