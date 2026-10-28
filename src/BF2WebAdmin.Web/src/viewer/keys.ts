// Free camera keys (KeyboardEvent.code). Kept free of three.js imports so the page can use them without
// pulling three.js into the initial bundle.

export const FORWARD = ['KeyW', 'ArrowUp']
export const BACK = ['KeyS', 'ArrowDown']
export const LEFT = ['KeyA', 'ArrowLeft']
export const RIGHT = ['KeyD', 'ArrowRight']
export const UP = ['KeyE', 'PageUp']
export const DOWN = ['KeyQ', 'PageDown']
export const BOOST = ['ShiftLeft', 'ShiftRight']
/** Held for slow, precise movement (Ctrl is not used: Ctrl+W/S/D are browser shortcuts) */
export const SLOW = ['AltLeft', 'AltRight']

export const SPECTATOR_KEYS = new Set([...FORWARD, ...BACK, ...LEFT, ...RIGHT, ...UP, ...DOWN, ...BOOST, ...SLOW])

/** Movement keys (not modifiers) - pressing one in another camera mode switches to the free camera */
export const isSpectatorMoveKey = (code: string) => SPECTATOR_KEYS.has(code) && !BOOST.includes(code) && !SLOW.includes(code)
