// The map builder's keys (KeyboardEvent.code), as in HeliGame's build mode: holding the right mouse button flies with
// WASD and Q/E; without it W and E pick the move and rotate tools. Space and Ctrl go up and down, and the arrow keys
// fly, either way. Kept free of three.js imports so the page can use them without pulling three.js into its bundle.

import { BOOST, SLOW } from '@/viewer/keys'

export const CTRL = ['ControlLeft', 'ControlRight']

export const BUILDER_KEY_MAP = {
  forward: ['KeyW', 'ArrowUp'],
  back: ['KeyS', 'ArrowDown'],
  left: ['KeyA', 'ArrowLeft'],
  right: ['KeyD', 'ArrowRight'],
  up: ['KeyE', 'Space', 'PageUp'],
  down: ['KeyQ', ...CTRL, 'PageDown'],
}

/** Keys that fly only while the right mouse button is held */
export const FLY_WHILE_LOOKING = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'KeyQ', 'KeyE'])

export const BUILDER_CAMERA_KEYS = new Set([...Object.values(BUILDER_KEY_MAP).flat(), ...BOOST, ...SLOW])
