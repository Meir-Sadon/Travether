/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// In development the API runs on :5080 (dotnet run) and Vite proxies /api and /hubs to it,
// so the browser sees one origin, the same as in production. /hubs carries the chat WebSocket.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': process.env.VITE_API_PROXY ?? 'http://localhost:5080',
      '/hubs': { target: process.env.VITE_API_PROXY ?? 'http://localhost:5080', ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: true,
  },
})
