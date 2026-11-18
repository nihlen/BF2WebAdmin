# Map builder assets

Turns the Battlefield 2 static objects into what the web map builder draws: one small Draco-compressed GLB per
spawnable object template and a `catalog.json`, in `src/BF2WebAdmin.Web/public/viewer/templates/`.

The output is made of the game's files, so it is not in git (`src/BF2WebAdmin.Web/.gitignore`), and neither are the 3D
viewer's maps and helicopter models beside it. A build of the web UI loads them from
`https://static.nihlen.net/bf2/viewer/v1/` (`templates/`, `maps/<map>/`, `models/`), or from the base in the
`VIEWER_ASSETS` environment variable. The Vite dev server uses the local copies in `public/viewer` when there are any.
Changed files go to a new version folder (`v2`, ...) with the base in `vite.config.ts`, because the site lets them be
cached for a year.

| File | What it does |
|---|---|
| `build_templates.py` | The build (a Blender script) |
| `bf2templates.py` | Reads the templates from the `.con`/`.tweak` files and resolves each to the meshes the server spawns for it |
| `bf2mesh.py` | `.staticmesh` and `.bundledmesh` readers, copied from BF2v2 `tools/replay-map` (read_bundledmesh returns numpy arrays here and reads version 6) |
| `verify_frame.py`, `verify_frame.mjs` | The frame check below |

## Build

Needs Blender 4.1 or later (4.4 was used) and the game's `objects_server.zip` and `objects_client.zip` extracted
into one folder as `Objects_server/` and `Objects_client/`, with PNG copies next to the client's DDS textures (as for
BF2v2's `build_map.py`). From the repository root:

```
blender -b --factory-startup --python tools/builder-assets/build_templates.py -- \
    --objects <folder with Objects_server and Objects_client> \
    --out src/BF2WebAdmin.Web/public/viewer/templates
```

It takes about ten seconds. Options: `--lod` (default 1) and `--lod0-max-tris` (default 1000) choose the level of
detail: LOD 0 when it has at most that many triangles (small props stay detailed up close), else LOD `--lod`, which
is what the replay map uses for its objects. `--only <template> ...` builds just those, without the catalog. A full
build deletes GLBs of templates that are no longer in the catalog.

What goes in: every template created under `staticobjects`, `vegitation`, `roads`, `water`, `common` (the flag pole
and the oil pump foundation; the light glows are left out) and `miscobjects` that has something to draw. Vehicles,
Weapons, Kits, soldiers, Effects, Develop and DynamicObjects are left out. What a template draws is what the server
spawns for it: its own geometry (a `.staticmesh`, or the 3P geometry of a `.bundledmesh` with its parts where the
child templates with `geometryPart` put them) plus its child templates (`addTemplate` with `setPosition` and
`setRotation`), recursively, all in one GLB. Templates with nothing to draw (sounds, supply points, effect bundles,
lights, window and ladder part templates) are left out silently; the others that cannot be built are listed under
`skipped` in the catalog with the reason: skinned meshes (flags, the clothes line, the animated oil pump), compiled
road templates (a level's roads, not a mesh), meshes missing from the extracted files and stand-in meshes of a few
centimetres (some ladders).

## Output

`<template>.glb`, the template name in lower case. Each has up to three meshes, named for how to draw them:

| Mesh | Draw it |
|---|---|
| `objects` | Opaque, vertex colours (`MeshLambertMaterial({ vertexColors: true })`, like the map's objects) |
| `see_through` | Alpha-tested surfaces of buildings and props (wire fences, grates, window panes): grey, translucent like the map's `see_through` |
| `leaves` | Vegetation leaf cards, coloured from the leaf texture: opaque and double sided |

Colours are an sRGB colour per triangle corner sampled from very low resolution copies of the textures (base x detail
x dirt), as BF2v2 `build_map.py` does for the map. Normals are smooth with edges sharper than 40 degrees kept.

`catalog.json`:

```
{"version": 1, "frame": "...",
 "templates": [
   {"name": "train_cargowag_01", "category": "staticobjects/industry", "type": "bundle",
    "min": [-1.743, -2.431, -8.997], "max": [1.743, 2.431, 8.997], "size": 18.962, "triangles": 1943, "lod": 1,
    "parts": 3}, ...],
 "skipped": {"flag_us": "geometry type not supported: flag_us (skinnedmesh)", ...}}
```

- `name`: the ObjectTemplate name, lower case: what `objecttemplate`/`object.create` take (BF2 names are not case
  sensitive) and the GLB's file name. It is the template's name, not its mesh's.
- `category`: the first two folders of the template's `.con` under `Objects_server`, lower case
  (`staticobjects/_asia`, `vegitation/middle-east`, `roads/staticsnoblend`, `water`).
- `type`: the template's class (`simpleobject`, `bundle`, `destroyableobject`, `ladder`, ...).
- `min`, `max`: the axis-aligned bounds of everything the GLB draws, in the template's own BF2 frame (metres, x right,
  y up, z forward), not mirrored. `size`: the bounds' diagonal, for sorting.
- `triangles`, `lod` (the highest LOD used), `parts` (only when the template is several meshes).

## Frame

A GLB's vertices are the template's BF2 local frame with z mirrored, like the replay map: BF2 local (x, y, z) is
(x, y, -z) in the GLB. Its origin is the template's pivot, the point `Object.absolutePosition` places. BF2 meshes are
mostly centred on their bounds, not standing on their base: `min[1]` is how far the base is below the pivot (a house
-2.9, a birch -8.9).

To place a template at a BF2 position `[x, h, z]` and rotation `[yaw, pitch, roll]` in degrees (`object.rotation`,
the values `StaticObjects.con` has):

```ts
model.position.set(x, h, -z)
model.rotation.set(degToRad(-pitch), degToRad(-yaw), degToRad(roll), 'YXZ')
```

That is `bf2ToWorld` and `rotToQuaternion` in `src/BF2WebAdmin.Web/src/viewer/coords.ts`. In BF2 terms an object's
point is `R(yaw, pitch, roll) @ local + position` with `R = Ry(yaw) Rx(pitch) Rz(roll)` (yaw 0 faces +z, north; 90
faces +x, east), as `build_map.py` places the map's objects; mirroring z turns that into three.js
`Ry(-yaw) Rx(-pitch) Rz(roll)`, which is Euler order `YXZ`. The map's flat objects (concrete blocks, helipads) are
lifted 0.2 to 0.35 m in `map.glb` against z-fighting; the template GLBs are not.

## Verification

The frame was checked against the replay map of Dalian: objects of `StaticObjects.con` placed with the transform above
in three.js, each GLB vertex measured to the nearest vertex of the map's objects (`map.glb`, built by `build_map.py`).
Only objects the map draws the same way can be compared: one mesh, at least 5 m, not flat, not vegetation (the map
draws stand-in trees), at the map's LOD 1. The objects with a pitch or roll are small enough that the shipped GLBs use
LOD 0 for them, so those were checked with a build at LOD 1 throughout (`--lod0-max-tris 0`, the same code otherwise):

```
blender -b --factory-startup --python tools/builder-assets/build_templates.py -- \
    --objects <objects folder> --out <scratch>/lod1 --lod0-max-tris 0
blender -b --factory-startup --python tools/builder-assets/verify_frame.py -- \
    --level <Dalian_2_v_2>/server/StaticObjects.con \
    --map src/BF2WebAdmin.Web/public/viewer/maps/dalian_2_v_2/map.glb \
    --templates <scratch>/lod1 --out <scratch>/frame-check.json
node tools/builder-assets/verify_frame.mjs <scratch>/frame-check.json
```

`verify_frame.mjs` uses the web UI's three.js (`npm ci` in `src/BF2WebAdmin.Web` first). Result on 2026-11-17, 25
objects (all 13 distinct pitched or rolled placements, 12 with only a yaw), some of them:

| Object | Rotation | Vertices | Mean | Max | Max with one sign flipped (yaw / pitch / roll) |
|---|---|---|---|---|---|
| fence_corrugated_3x12m | -177.3/13.4/4 | 102 | 1.3 cm | 2.1 cm | 0.58 / 0.81 / 0.85 m |
| pipeline_30m | -179.9/6.2/21.8 | 326 | 1.2 cm | 1.8 cm | 0.06 / 1.30 / 11.02 m |
| container_cl_yellow | -18.5/0/180 | 598 | 1.3 cm | 2.1 cm | 2.32 m / - / 0.02 m |
| fence_corrugated_3x12m | -90/-69/0 | 102 | 1.2 cm | 2.0 cm | 2.30 / 2.27 m / - |
| fence_corrugated_3x12m | 90/-89.5/-180 | 102 | 1.1 cm | 1.9 cm | 1.62 / 1.73 / 0.02 m |
| pipeline_curvedown | -177.4/6.4/1.3 | 302 | 1.2 cm | 2.0 cm | 1.28 / 1.08 / 0.68 m |
| pipestack_01 | 4.6/-8/0 | 456 | 1.2 cm | 1.9 cm | 0.41 / 0.27 m / - |
| mi_hangar_us_v2 | 150.7/0/0 | 4552 | 1.2 cm | 2.1 cm | 5.84 m |
| wirefence_48m | 153.6/0/0 | 3592 | 1.1 cm | 2.0 cm | 19.05 m |
| oilcistern_02 | -141.9/0/0 | 3104 | 1.2 cm | 2.1 cm | 6.24 m |

Every vertex of all 25 is within 2.1 cm of the map's, about the map's own Draco quantization (16 bits over the
level's objects). Flipping the sign of an angle moves them by up to 19 m; where a flip barely matters the rotation is
symmetric in that angle (roll 180 is roll -180, yaw -179.9 is nearly yaw 179.9). The shipped GLBs (the 12 yaw-only
objects, all at LOD 1 in the shipped build) give the same: at most 2.2 cm. Children of bundles are placed with the
same rotation code but cannot be compared: `map.glb` draws only a template's own mesh.
