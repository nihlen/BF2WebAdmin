import fs from 'node:fs'
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig, type Plugin } from 'vite'
import { fixtures } from './fixtures-plugin.ts'

// The BF2WebAdmin.Server URL. Aspire (BF2WebAdmin.AppHost) passes it in; without Aspire, the server's launch profile
// listens on http://localhost:5001.
const serverUrl = process.env.SERVER_HTTPS ?? process.env.SERVER_HTTP ?? 'http://localhost:5001'

// The 3D viewer's and map builder's models (maps, helicopters, object templates) are built from the game's files and are
// not in the repo: they are served from static.nihlen.net (tools/builder-assets/README.md). VIEWER_ASSETS sets another
// base, such as /viewer for copies in public/viewer. In development those local copies are used when there are any.
// This is the build's default; WebAdmin's StaticAssets:ViewerBaseUrl replaces it at runtime (src/lib/static-assets.ts).
const staticAssets = 'https://static.nihlen.net/bf2/viewer/v1'
const localAssets = '/viewer'
const assetFolders = ['maps', 'models', 'templates']
const hasLocalAssets = assetFolders.some((f) => fs.existsSync(path.resolve(import.meta.dirname, 'public/viewer', f)))

// This folder as the file system spells it. Started from a working directory with another drive-letter case
// (c:\Data\... for C:\Data\..., as some Windows shells and MSBuild pass it), the app's own modules would get c:\ ids and
// the packages' C:\ ids (the resolver follows the real path), so node_modules\react was bundled twice and a hook ran on
// the copy that was not rendering ("Cannot read properties of null (reading 'useMemo')"). The root and the alias from
// the real path keep one id per file.
const here = fs.realpathSync.native(import.meta.dirname)

// UI_FIXTURES=1: the dev server answers the API from fixed data and fakes the hub, with no server (fixtures-plugin.ts)
const useFixtures = process.env.UI_FIXTURES === '1'

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
    root: here,
    define: { __VIEWER_ASSETS__: JSON.stringify(viewerAssets) },
    plugins: [...(useFixtures ? [fixtures()] : []), react(), tailwindcss(), dropLocalAssets(viewerAssets)],
    resolve: {
      alias: {
        '@': path.resolve(here, './src'),
      },
    },
    server: {
      port: process.env.PORT ? Number(process.env.PORT) : 5173,
      // The API and the hub are on the server; the proxy keeps them on the page's origin, so the auth cookie works as in
      // production, where the server serves this app from wwwroot.
      proxy: useFixtures ? undefined : {
        '/api': { target: serverUrl, changeOrigin: true, secure: false },
        '/hubs': { target: serverUrl, changeOrigin: true, secure: false, ws: true },
      },
    },
  }
})
