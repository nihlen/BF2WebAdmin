"""
Builds the map builder's object models: one Draco-compressed GLB per spawnable static object template and a
catalog.json, from an extracted BF2 objects folder. See README.md for the command and the frame convention.

Run with Blender 4.1 or later (it bundles numpy and the glTF exporter):

    blender -b --factory-startup --python tools/builder-assets/build_templates.py -- \
        --objects <folder with Objects_server/ and Objects_client/> \
        --out src/BF2WebAdmin.Web/public/viewer/templates

What the server spawns for a template (bf2templates.py): its own geometry (a .staticmesh, or the 3P geometry of a
.bundledmesh with its parts where the child templates put them) and its child templates' geometry (addTemplate,
setPosition, setRotation) at their offsets, recursively. One GLB holds all of it, in the template's own frame.

The meshes are coloured like the replay map's objects (BF2v2 tools/replay-map/build_map.py, whose texture sampling
this copies): an sRGB colour per triangle corner sampled from very low resolution copies of the textures, base x
detail x dirt. Each GLB has up to three meshes, named for how to draw them:
    objects      opaque, vertex coloured
    see_through  alpha tested surfaces of buildings and props (wire fences, grates, window panes), grey; the holes
                 are in the texture's alpha, which the GLB does not carry, so draw them translucent
    leaves       vegetation leaf cards (alpha tested in the game), coloured from the leaf texture's opaque texels;
                 draw them opaque and double sided

Coordinates: the GLB's frame is the template's BF2 frame mirrored like the map: a BF2 local point (x, y, z) is at
(x, y, -z) in the GLB (glTF/three.js Y-up). Bounds in catalog.json are in BF2 metres, unmirrored.
"""

import argparse
import json
import math
import os
import re
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from bf2mesh import read_bundledmesh, read_staticmesh  # noqa: E402
from bf2templates import MeshIndex, Templates  # noqa: E402

# Top folders of Objects_server whose templates go into the catalog (lower case). Vehicles, Weapons, Kits,
# soldiers, Effects, Develop and DynamicObjects are left out: they are not static props
INCLUDE_FOLDERS = ("staticobjects", "vegitation", "roads", "water", "common", "miscobjects")
# Light glows (sprites drawn by lamps) are not props
EXCLUDE_CATEGORIES = ("common/glow",)
# Some ladders and supply points carry a tiny stand-in mesh (a few centimetres) instead of a model
MIN_SIZE = 0.1

# --- Copied from build_map.py: texture sampling and colours ---
COLOR_LAYERS = ("Base", "Detail", "Dirt", "Crack")
TEXTURE_DOWNSCALE = 16
DETAIL_GAIN = 1.6
DEFAULT_OBJECT_COLOR = (0.62, 0.6, 0.56)
SEE_THROUGH_COLOR = (0.62, 0.62, 0.62)
SMOOTH_ANGLE = 40
LEAF_TINT = np.array([0.75, 1.1, 0.8])
# --- Own ---
DEFAULT_LEAF_COLOR = np.array([0.29, 0.31, 0.18])


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--objects", required=True, help="Folder with Objects_server/ and Objects_client/")
    p.add_argument("--out", required=True, help="Output folder for <template>.glb and catalog.json")
    p.add_argument("--lod", type=int, default=1, help="Mesh LOD to use (0 = most detailed)")
    p.add_argument("--lod0-max-tris", type=int, default=1000,
                   help="Use LOD 0 instead when its triangles are at most this many (small props stay detailed up close)")
    p.add_argument("--only", nargs="*", help="Build only these templates (catalog.json is then not written)")
    return p.parse_args(argv)


# ---------------------------------------------------------------------------------------------------------------------
# Textures (from build_map.py)

class TextureFiles:
    """Texture PNGs (by the path in the mesh) under Objects_client, loaded as tiny sRGB arrays"""

    def __init__(self, client_root):
        self.pngs = {}
        for dirpath, _, names in os.walk(client_root):
            for n in names:
                if n.lower().endswith(".png"):
                    path = os.path.join(dirpath, n)
                    self.pngs[os.path.relpath(path, client_root).replace("\\", "/").lower()] = path
        self._textures = {}
        self._mipmaps = {}

    def texture(self, path):
        """Texture as a tiny HxWx4 sRGB array (row 0 = top, like D3D texture coordinates), or None if missing"""
        key = re.sub(r"^objects/", "", path.replace("\\", "/").lower()).rsplit(".", 1)[0] + ".png"
        if key not in self._textures:
            pixels = None
            if key in self.pngs:
                image = bpy.data.images.load(self.pngs[key])
                w, h = image.size
                image.scale(max(4, w // TEXTURE_DOWNSCALE), max(4, h // TEXTURE_DOWNSCALE))
                w, h = image.size
                pixels = np.empty(w * h * 4, dtype=np.float32)
                image.pixels.foreach_get(pixels)
                pixels = pixels.reshape(h, w, 4)[::-1].copy()
                bpy.data.images.remove(image)
            self._textures[key] = pixels
        return self._textures[key]

    def mipmaps(self, path):
        if path not in self._mipmaps:
            texture = self.texture(path)
            self._mipmaps[path] = None if texture is None else mip_chain(texture)
        return self._mipmaps[path]


def sample(texture, uv):
    """Bilinear sample for each uv (Nx2), wrapping like a tiled texture"""
    h, w = texture.shape[:2]
    x = np.mod(uv[:, 0], 1) * w - 0.5
    y = np.mod(uv[:, 1], 1) * h - 0.5
    x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = (x - x0)[:, None], (y - y0)[:, None]
    x0, x1 = x0 % w, (x0 + 1) % w
    y0, y1 = y0 % h, (y0 + 1) % h
    top = texture[y0, x0] * (1 - fx) + texture[y0, x1] * fx
    bottom = texture[y1, x0] * (1 - fx) + texture[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def mip_chain(texture):
    levels = [texture]
    while levels[-1].shape[0] > 1 or levels[-1].shape[1] > 1:
        t = levels[-1]
        h, w = max(t.shape[0] // 2, 1), max(t.shape[1] // 2, 1)
        fy, fx = t.shape[0] // h, t.shape[1] // w
        levels.append(t[: h * fy, : w * fx].reshape(h, fy, w, fx, 4).mean(axis=(1, 3)))
    return levels


def sample_mipmapped(levels, uv):
    """Bilinear sample for each triangle corner (uv: Mx3x2) from the mip level that fits the triangle's footprint"""
    h, w = levels[0].shape[:2]
    span = uv.max(axis=1) - uv.min(axis=1)
    texels = np.maximum(span[:, 0] * w, span[:, 1] * h)
    level = np.clip(np.ceil(np.log2(np.maximum(texels, 1))), 0, len(levels) - 1).astype(np.int64)
    result = np.empty((len(uv), 3, 4), dtype=np.float32)
    for l in np.unique(level):
        sel = level == l
        result[sel] = sample(levels[l], uv[sel].reshape(-1, 2)).reshape(-1, 3, 4)
    return result.reshape(-1, 4)


def material_colors(material, uvs, tris, files):
    """sRGB colour per triangle corner (Mx3x3) for a static mesh material: base x detail x dirt"""
    colors = np.tile(np.array(DEFAULT_OBJECT_COLOR, dtype=np.float32), (len(tris) * 3, 1))
    layers = re.findall(r"N?[A-Z][a-z]*", material["technique"])
    uv_set = 0
    for layer, path in zip(layers, material["maps"]):
        if layer not in COLOR_LAYERS:
            continue
        uv_index, uv_set = uv_set, uv_set + 1
        levels = files.mipmaps(path)
        if levels is None or uv_index >= len(uvs) or layer == "Crack":
            continue
        rgb = sample_mipmapped(levels, uvs[uv_index][tris])[:, :3]
        if layer == "Base":
            colors = rgb
        elif layer == "Detail":
            colors = colors * rgb * DETAIL_GAIN
        else:
            colors = colors * rgb
    return np.clip(colors, 0, 1).reshape(-1, 3, 3)


def texture_mean(files, path):
    """Average sRGB colour of the opaque texels of a texture"""
    texture = files.texture(path)
    if texture is None:
        return None
    opaque = texture[texture[..., 3] > 0.5]
    return (opaque if len(opaque) else texture.reshape(-1, 4))[:, :3].mean(axis=0)


# ---------------------------------------------------------------------------------------------------------------------
# Meshes

def triangle_count(lod):
    return sum(m["inum"] // 3 for m in lod["materials"])


def pick_lod(lods, args):
    """The LOD to use from a geometry's LODs: args.lod, or LOD 0 when it is small enough"""
    if triangle_count(lods[0]) <= args.lod0_max_tris:
        return lods[0]
    return lods[min(args.lod, len(lods) - 1)]


class Parts:
    """Triangles of one GLB mesh: vertices (BF2 template frame), triangles, sRGB corner colours, smooth flags"""

    def __init__(self):
        self.verts, self.faces, self.colors, self.smooth, self.offset = [], [], [], [], 0

    def __bool__(self):
        return self.offset > 0

    def add(self, verts, faces, colors, smooth):
        if len(faces) == 0:
            return
        # Keep only the vertices these triangles use
        used, faces = np.unique(faces, return_inverse=True)
        faces = faces.reshape(-1, 3)
        self.verts.append(verts[used])
        self.faces.append(faces + self.offset)
        self.colors.append(colors)
        self.smooth.append(np.full(len(faces), smooth))
        self.offset += len(used)

    def arrays(self):
        return np.concatenate(self.verts), np.concatenate(self.faces), np.concatenate(self.colors), np.concatenate(self.smooth)


def leaf_colors(files, material, verts, tris):
    """Leaf cards: the leaf texture's opaque colour pushed towards green, darker at the bottom of the crown"""
    leaf = texture_mean(files, material["maps"][0]) if material["maps"] else None
    leaf = np.clip((leaf if leaf is not None else DEFAULT_LEAF_COLOR) * LEAF_TINT, 0, 1)
    heights = verts[tris][..., 1]
    bottom, top = verts[:, 1].min(), verts[:, 1].max()
    k = np.clip((heights - bottom) / max(top - bottom, 0.1), 0, 1)[..., None]
    return np.clip(leaf * (0.65 + 0.5 * k), 0, 1)


_mesh_cache = {}


def mesh_triangles(piece, files, args):
    """
    The triangles of one piece in the piece's own frame: [(target, verts, tris, colors)] with target objects,
    see_through or leaves. Triangles are in Blender winding (the D3D winding reversed: the swap into Blender space
    mirrors the mesh, which flips it back), like build_map.py
    """
    key = (piece["path"], tuple(sorted((k, tuple(np.round(r, 6).ravel()), tuple(np.round(p, 6))) for k, (r, p) in piece.get("parts", {}).items())))
    if key in _mesh_cache:
        return _mesh_cache[key]
    vegetation = "/vegitation/" in piece["path"].replace("\\", "/").lower()
    result = []
    if piece["kind"] == "static":
        mesh = read_staticmesh(piece["path"])
        verts = mesh["positions"].astype(np.float64)
        lods = [l for l in mesh["lods"] if l["geom"] == 0]
        uvs = mesh["uvs"]
    else:
        mesh = read_bundledmesh(piece["path"])
        verts = mesh["positions"].astype(np.float64).copy()
        # Geometry 0 of a multi-geometry bundled mesh is the 1P view (a placeholder for buildings); 1 is the 3P
        geom = 1 if len(mesh["geoms"]) > 1 else 0
        lods = [l for l in mesh["lods"] if l["geom"] == geom]
        uvs = mesh["uvs"][:1]
        for part, (r, p) in piece.get("parts", {}).items():
            sel = mesh["part"] == part
            verts[sel] = verts[sel] @ r.T + p
    lod = pick_lod(lods, args)
    for m in lod["materials"]:
        t = mesh["indices"][m["istart"]: m["istart"] + m["inum"]].reshape(-1, 3) + m["vstart"]
        t = t[:, [0, 2, 1]]
        if len(t) == 0:
            continue
        if m["alpha"] != 0 and vegetation:
            result.append(("leaves", verts, t, leaf_colors(files, m, verts, t)))
        elif m["alpha"] != 0:
            result.append(("see_through", verts, t, np.tile(np.array(SEE_THROUGH_COLOR), (len(t), 3, 1))))
        elif piece["kind"] == "static":
            result.append(("objects", verts, t, material_colors(m, uvs, t, files)))
        else:
            # Bundled meshes: the colour map is the first texture, on the first texture coordinate set
            m2 = dict(m, technique="Base")
            result.append(("objects", verts, t, material_colors(m2, uvs, t, files)))
    _mesh_cache[key] = (result, triangle_count(lod), lods.index(lod))
    return _mesh_cache[key]


# ---------------------------------------------------------------------------------------------------------------------
# Blender

def bf2_to_blender(points):
    """(x, height, z) in BF2 -> (x, z, height) in Blender; the glTF exporter then makes it (x, height, -z)"""
    return points[:, [0, 2, 1]]


def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)


def make_vertex_color_material(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 1.0
    node = mat.node_tree.nodes.new("ShaderNodeVertexColor")
    node.layer_name = "Color"
    mat.node_tree.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    if name == "leaves":
        mat.use_backface_culling = False  # exported as doubleSided
    return mat


def create_mesh_object(name, verts, faces, colors, smooth):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(bf2_to_blender(verts).tolist(), [], faces.tolist())
    rgba = np.ones((len(colors) * 3, 4), dtype=np.float32)
    rgba[:, :3] = srgb_to_linear(colors.reshape(-1, 3))
    attr = mesh.color_attributes.new("Color", "FLOAT_COLOR", "CORNER")
    attr.data.foreach_set("color", rgba.ravel())
    mesh.validate()
    mesh.update()
    if name == "objects":
        mesh.shade_smooth()
        mesh.set_sharp_from_angle(angle=math.radians(SMOOTH_ANGLE))
    else:
        mesh.polygons.foreach_set("use_smooth", smooth.astype(bool))
    mesh.materials.append(make_vertex_color_material(name))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def export_glb(path):
    bpy.ops.export_scene.gltf(
        filepath=path,
        export_format="GLB",
        export_draco_mesh_compression_enable=True,
        export_draco_mesh_compression_level=7,
        export_draco_position_quantization=14,
        export_draco_normal_quantization=8,
        export_draco_color_quantization=8,
        export_normals=True,
        export_texcoords=False,
        export_materials="EXPORT",
        export_apply=True,
        export_yup=True,
        export_extras=False,
    )


# ---------------------------------------------------------------------------------------------------------------------

def catalog_candidates(templates):
    names = []
    for name, t in templates.templates.items():
        if not t["created"]:
            continue
        top = t["file"].split("/", 1)[0].lower()
        if top in INCLUDE_FOLDERS and templates.category(name) not in EXCLUDE_CATEGORIES:
            names.append(name)
    return sorted(names)


def main():
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    server = os.path.join(args.objects, "Objects_server")
    client = os.path.join(args.objects, "Objects_client")
    templates = Templates(server)
    index = MeshIndex(client)
    files = TextureFiles(client)
    os.makedirs(args.out, exist_ok=True)

    names = [n.lower() for n in args.only] if args.only else catalog_candidates(templates)
    print(f"BUILD {len(templates.templates)} templates parsed, {len(names)} candidates")
    entries, skipped = [], {}
    started = time.time()
    for i, name in enumerate(names):
        t = templates.templates.get(name)
        if t is None:
            skipped[name] = "unknown template"
            continue
        missing = set()
        pieces = templates.pieces(name, index, missing=missing)
        if not pieces:
            unsupported = sorted(m for m in missing if "(" in m)
            if unsupported:
                skipped[name] = "geometry type not supported: " + ", ".join(unsupported)
            elif missing:
                skipped[name] = "mesh file not found: " + ", ".join(sorted(missing))
            else:
                skipped[name] = "no geometry"
            continue
        groups = {"objects": Parts(), "see_through": Parts(), "leaves": Parts()}
        triangles, lods = 0, set()
        for piece in pieces:
            result, _, lod = mesh_triangles(piece, files, args)
            lods.add(lod)
            for target, verts, tris, colors in result:
                world = verts @ piece["rotation"].T + piece["offset"]
                groups[target].add(world, tris, colors, target == "leaves")
                triangles += len(tris)
        if triangles == 0:
            skipped[name] = "empty mesh"
            continue
        meshes = {target: parts.arrays() for target, parts in groups.items() if parts}
        allv = np.concatenate([m[0] for m in meshes.values()])
        lo, hi = allv.min(axis=0), allv.max(axis=0)
        if np.linalg.norm(hi - lo) < MIN_SIZE:
            skipped[name] = f"placeholder mesh ({np.linalg.norm(hi - lo):.3f} m)"
            continue
        clear_scene()
        for target, (verts, faces, colors, smooth) in meshes.items():
            create_mesh_object(target, verts, faces, colors, smooth)
        path = os.path.join(args.out, f"{name}.glb")
        export_glb(path)
        entry = {
            "name": name,
            "category": templates.category(name),
            "type": t["type"],
            "min": [round(float(v), 3) for v in lo],
            "max": [round(float(v), 3) for v in hi],
            "size": round(float(np.linalg.norm(hi - lo)), 3),
            "triangles": int(triangles),
            "lod": max(lods),
            "bytes": os.path.getsize(path),
        }
        if len(pieces) > 1:
            entry["parts"] = len(pieces)
        entries.append(entry)
        if (i + 1) % 50 == 0:
            print(f"BUILD {i + 1}/{len(names)} {time.time() - started:.0f} s")

    total = sum(e["bytes"] for e in entries)
    # Sounds, supply points, effect bundles, ladders' and windows' part templates: nothing to draw
    no_geometry = {}
    for name, why in skipped.items():
        if why == "no geometry":
            kind = templates.templates[name]["type"]
            no_geometry[kind] = no_geometry.get(kind, 0) + 1
    skipped = {k: v for k, v in skipped.items() if v != "no geometry"}
    print(f"BUILD {len(entries)} templates, {total / 1024 / 1024:.2f} MB of GLB, "
          f"{sum(e['triangles'] for e in entries)} triangles, {time.time() - started:.0f} s")
    print(f"BUILD without geometry (left out): {sum(no_geometry.values())} " + ", ".join(f"{k} {v}" for k, v in sorted(no_geometry.items())))
    for name, why in sorted(skipped.items()):
        print(f"BUILD skipped {name}: {why}")
    if args.only:
        for e in entries:
            print("BUILD", json.dumps(e))
        return
    # Models of templates that are gone from the catalog
    built = {f"{e['name']}.glb" for e in entries}
    for n in os.listdir(args.out):
        if n.endswith(".glb") and n not in built:
            os.remove(os.path.join(args.out, n))
    catalog = {
        "version": 1,
        "frame": "BF2 template frame: x right, y up, z forward, metres. GLB = (x, y, -z); see tools/builder-assets/README.md",
        "templates": sorted(entries, key=lambda e: e["name"]),
        "skipped": {k: v for k, v in sorted(skipped.items())},
    }
    for e in catalog["templates"]:
        e.pop("bytes")
    # One template per line: small, and diffs well
    with open(os.path.join(args.out, "catalog.json"), "w", newline="\n") as f:
        f.write('{"version":1,\n"frame":' + json.dumps(catalog["frame"]) + ',\n"templates":[\n')
        f.write(",\n".join(json.dumps(e, separators=(",", ":")) for e in catalog["templates"]))
        f.write('\n],\n"skipped":' + json.dumps(catalog["skipped"], indent=0) + "}\n")
    print(f"BUILD wrote {os.path.join(args.out, 'catalog.json')}")


main()
