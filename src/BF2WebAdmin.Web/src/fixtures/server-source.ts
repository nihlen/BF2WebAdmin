// What the fixture mode reads from BF2WebAdmin.Server's C# source, so its modules and commands say what the real
// server's do: the [ModuleInfo] and [LoadedWith] attributes of the modules and the command types each one handles
// (src/BF2WebAdmin.Server/Modules), and the Description of each [Command] (src/BF2WebAdmin.Server/Commands). The plugin
// reads the files; this parses their text, with no Node APIs, as data.ts does.

import type { components } from '../api/schema.d.ts'

type S = components['schemas']

export type SourceFile = { path: string; text: string }

export type ServerSource = {
  /** Each module class: its name and description in words, the module it comes with, the command types it handles */
  modules: Map<string, { displayName: string | null; description: string | null; loadedWith: string | null; handles: string[] }>
  /** Each command class's Description */
  descriptions: Map<string, string>
}

const unescape = (text: string) => text.replace(/\\(.)/g, '$1')
const quoted = String.raw`"((?:[^"\\]|\\.)*)"`

export function parseServerSource(moduleFiles: SourceFile[], commandFiles: SourceFile[]): ServerSource {
  const modules: ServerSource['modules'] = new Map()
  for (const { text } of moduleFiles) {
    // One module class per file
    const declaration = /public\s+class\s+(\w+Module)\b([^{]*)\{/.exec(text)
    if (!declaration) continue
    const [, name, bases] = declaration
    const info = new RegExp(String.raw`\[ModuleInfo\(${quoted},\s*${quoted}\)\]`).exec(text)
    modules.set(name, {
      displayName: info ? unescape(info[1]) : null,
      description: info ? unescape(info[2]) : null,
      loadedWith: /\[LoadedWith\(typeof\((\w+)\)\)\]/.exec(text)?.[1] ?? null,
      handles: [...bases.matchAll(/IHandleCommand(?:Async)?<(\w+)>/g)].map((m) => m[1]),
    })
  }

  const descriptions = new Map<string, string>()
  const command = new RegExp(String.raw`\[Command\([^\]]*?Description\s*=\s*${quoted}\)\]\s*public\s+(?:sealed\s+)?class\s+(\w+)`, 'g')
  for (const { text } of commandFiles) for (const m of text.matchAll(command)) descriptions.set(m[2], unescape(m[1]))

  return { modules, descriptions }
}

/** A module's name in words when it has no [ModuleInfo], as ModuleResolver.DisplayNameOf makes it. */
const displayNameOf = (source: ServerSource, name: string) =>
  source.modules.get(name)?.displayName ??
  name
    .replace(/Module$/, '')
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace(/([a-z]{2,})(\d)/g, '$1 $2')

/** The modules that come with a module, and those that come with them. */
export function loadedWith(source: ServerSource, module: string): string[] {
  const direct = [...source.modules].filter(([, m]) => m.loadedWith === module).map(([name]) => name)
  return direct.flatMap((name) => [name, ...loadedWith(source, name)])
}

/**
 * GET /api/modules as ModuleResolver.GetModuleInfos answers it: the modules a group can enable (not those that come with
 * another), by class name, each with the commands it and the modules that come with it handle (the longest alias of
 * each, in the order of commands.txt) and the names of those modules.
 */
export function moduleInfos(source: ServerSource, commands: { className: string; aliases: string[] }[]): S['ModuleInfo'][] {
  return [...source.modules]
    .filter(([, m]) => !m.loadedWith)
    .map(([name]) => name)
    .sort()
    .map((name) => {
      const withIt = [...new Set(loadedWith(source, name))].sort()
      const handled = new Set([name, ...withIt].flatMap((m) => source.modules.get(m)?.handles ?? []))
      const aliases = commands.filter((c) => handled.has(c.className)).map((c) => `.${c.aliases.reduce((a, b) => (b.length > a.length ? b : a))}`)
      return {
        name,
        displayName: displayNameOf(source, name),
        description: source.modules.get(name)?.description ?? null,
        commands: [...new Set(aliases)],
        loadedWith: withIt.map((m) => displayNameOf(source, m)),
      }
    })
}
