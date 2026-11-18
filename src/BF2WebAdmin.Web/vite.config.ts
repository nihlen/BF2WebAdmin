import fs from 'node:fs'
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'

// The BF2WebAdmin.Server URL. Aspire (BF2WebAdmin.AppHost) passes it in; without Aspire, the server's launch profile
// listens on http://localhost:5001.
const serverUrl = process.env.SERVER_HTTPS ?? process.env.SERVER_HTTP ?? 'http://localhost:5001'

// The 3D viewer's and map builder's models (maps, helicopters, object templates) are built from the game's files and are
// not in the repo: they are served from static.nihlen.net (tools/builder-assets/README.md). VIEWER_ASSETS sets another
// base, such as /viewer for copies in public/viewer. In development those local copies are used when there are any.
const staticAssets = 'https://static.nihlen.net/bf2/viewer/v1'
const localAssets = '/viewer'
const assetFolders = ['maps', 'models', 'templates']
const hasLocalAssets = assetFolders.some((f) => fs.existsSync(path.resolve(import.meta.dirname, 'public/viewer', f)))

/** Leaves the local copies out of a build that loads the models from elsewhere: Vite copies all of public into dist */
function dropLocalAssets(base: string): Plugin {
  return {
    name: 'drop-local-viewer-assets',
    apply: 'build',
    closeBundle() {
      if (base === localAssets) return
      for (const folder of assetFolders) fs.rmSync(path.resolve(import.meta.dirname, 'dist/viewer', folder), { recursive: true, force: true })
    },
  }
}

// https://vite.dev/config/
export default defineConfig(({ command }) => {
  const viewerAssets = process.env.VIEWER_ASSETS ?? (command === 'serve' && hasLocalAssets ? localAssets : staticAssets)
  return {
    define: { __VIEWER_ASSETS__: JSON.stringify(viewerAssets) },
    plugins: [react(), tailwindcss(), dropLocalAssets(viewerAssets)],
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
  }
})
