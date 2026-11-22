import type { CatalogEntry } from '@/builder/templates'

// How the map builder's palette groups and remembers the object templates

/**
 * The chip a template's category belongs to. The static objects (most of the catalog) by their folder, such as
 * "staticobjects/_asia"; vegetation, roads and water as one chip each, whatever their sub-folder; the few others
 * (flags, an oil pump) together, so the chips stay a short row.
 */
export function categoryKey(category: string) {
  const top = category.split('/')[0]
  if (top === 'staticobjects') return category.split('/').slice(0, 2).join('/')
  return ['vegitation', 'roads', 'water'].includes(top) ? top : 'other'
}

/**
 * Roads, airstrips and water planes: flat pieces that lie on the terrain and are the biggest templates, so sorted by
 * size they would fill the top of the list. Hidden until asked for.
 */
export const isGroundOrWater = (entry: CatalogEntry) => /^(roads|water)(\/|$)/.test(entry.category)

const labels: Record<string, string> = { vegitation: 'Vegetation', roads: 'Roads', water: 'Water', other: 'Other' }

/** A chip's name in words: "staticobjects/_middle-east" → "Middle East", "vegitation" → "Vegetation". */
export function categoryLabel(key: string) {
  const folder = key.split('/')[1]
  if (!folder) return labels[key] ?? key
  return folder.replace(/^_+/, '').replace(/[-_]+/g, ' ').replace(/\b[a-z]/g, (c) => c.toUpperCase())
}

const recentKey = 'builder.recentTemplates'
const recentCount = 6

/** The templates placed last in this browser, newest first */
export function readRecent(): string[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(recentKey) ?? '[]')
    return Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string').slice(0, recentCount) : []
  } catch {
    return []
  }
}

/** <template> first in the list, which is remembered in this browser */
export function addRecent(recent: string[], template: string) {
  const next = [template, ...recent.filter((t) => t !== template)].slice(0, recentCount)
  try {
    localStorage.setItem(recentKey, JSON.stringify(next))
  } catch {
    // Not remembered, that is all
  }
  return next
}
