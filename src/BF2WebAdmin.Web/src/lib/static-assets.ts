import { fetchClient } from '@/api/client'

/**
 * Where the files that are not in this repo come from: the minimap images and the 3D viewer's and map builder's
 * models. WebAdmin's configuration may name other places (StaticAssets:MinimapBaseUrl and StaticAssets:ViewerBaseUrl,
 * GET /api/config); without them, static.nihlen.net, or for the models the build's base (VIEWER_ASSETS, vite.config.ts).
 *
 * index.html sets the referrer policy same-origin: static.nihlen.net's Cloudflare refuses images whose Referer names
 * another site (hotlink protection, error 1011), and every self-hosted WebAdmin is another site.
 */
const defaults = {
  minimapBaseUrl: 'https://static.nihlen.net/bf2/minimaps',
  viewerBaseUrl: __VIEWER_ASSETS__,
}

let current = defaults

// An unreachable server must not keep the UI from starting: it then shows its own errors
const configTimeoutMs = 3_000

/** Reads the configured bases once, before the UI renders; on any failure the defaults stay */
export async function loadStaticAssets() {
  try {
    const { data } = await fetchClient.GET('/api/config', { signal: AbortSignal.timeout(configTimeoutMs) })
    current = {
      minimapBaseUrl: data?.minimapBaseUrl ?? defaults.minimapBaseUrl,
      viewerBaseUrl: data?.viewerBaseUrl ?? defaults.viewerBaseUrl,
    }
  } catch {
    // The defaults
  }
}

/** The minimap image of a map */
export const minimapUrl = (map: string) => `${current.minimapBaseUrl}/${map.toLowerCase()}.png`

/** The folder with maps/, models/ and templates/ of the 3D viewer and the map builder */
export const viewerAssets = () => current.viewerBaseUrl
