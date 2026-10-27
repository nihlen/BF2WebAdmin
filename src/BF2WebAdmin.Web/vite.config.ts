import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The BF2WebAdmin.Server URL. Aspire (BF2WebAdmin.AppHost) passes it in; without Aspire, the server's launch profile
// listens on http://localhost:5001.
const serverUrl = process.env.SERVER_HTTPS ?? process.env.SERVER_HTTP ?? 'http://localhost:5001'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: process.env.PORT ? Number(process.env.PORT) : 5173,
    // The API and the hub are on the server; the proxy keeps them on the page's origin, so the auth cookie works as in
    // production, where the server serves this app from wwwroot.
    proxy: {
      '/api': { target: serverUrl, changeOrigin: true, secure: false },
      '/hubs': { target: serverUrl, changeOrigin: true, secure: false, ws: true },
    },
  },
})
