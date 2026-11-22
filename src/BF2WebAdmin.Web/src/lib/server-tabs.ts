import { Ban, Box, Hammer, HeartPulse, History, Radio, Settings } from 'lucide-react'

/**
 * A server's pages, in the order of the desktop tab bar; the breadcrumb names the open one from here as well. The
 * phone's bottom bar shows the ones marked `phone` by their short label, and the rest under More.
 */
export const serverTabs = [
  { to: '/server/$serverId', label: 'Live', short: 'Live', icon: Radio, exact: true, phone: true },
  { to: '/server/$serverId/health', label: 'Health', short: 'Health', icon: HeartPulse, phone: true },
  { to: '/server/$serverId/bans', label: 'Bans', short: 'Bans', icon: Ban, phone: false },
  { to: '/server/$serverId/activity', label: 'Activity', short: 'Activity', icon: History, phone: false },
  { to: '/server/$serverId/viewer', label: '3D viewer', short: '3D', icon: Box, phone: true },
  { to: '/server/$serverId/builder', label: 'Map builder', short: 'Builder', icon: Hammer, phone: true },
  { to: '/server/$serverId/edit', label: 'Settings', short: 'Settings', icon: Settings, phone: false },
] as const
