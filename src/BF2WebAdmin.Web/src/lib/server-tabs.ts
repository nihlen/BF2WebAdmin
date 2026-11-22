import { Ban, Box, Hammer, HeartPulse, History, Radio, Settings } from 'lucide-react'

/** A server's pages, in the order of its tab bar; the breadcrumb names the open one from here as well. */
export const serverTabs = [
  { to: '/server/$serverId', label: 'Live', icon: Radio, exact: true },
  { to: '/server/$serverId/health', label: 'Health', icon: HeartPulse },
  { to: '/server/$serverId/bans', label: 'Bans', icon: Ban },
  { to: '/server/$serverId/activity', label: 'Activity', icon: History },
  { to: '/server/$serverId/viewer', label: '3D viewer', icon: Box },
  { to: '/server/$serverId/builder', label: 'Map builder', icon: Hammer },
  { to: '/server/$serverId/edit', label: 'Settings', icon: Settings },
] as const
