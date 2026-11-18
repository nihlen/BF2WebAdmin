"""
First half of the frame check (README.md, "Verification"): collects the vertices that verify_frame.mjs compares.

    blender -b --factory-startup --python tools/builder-assets/verify_frame.py -- \
        --level <level>/server/StaticObjects.con \
        --map src/BF2WebAdmin.Web/public/viewer/maps/<level>/map.glb \
        --templates src/BF2WebAdmin.Web/public/viewer/templates \
        --out <scratch>/frame-check.json

Picks objects of the level that the map has drawn the same way as the template GLBs (one mesh, at the same LOD as
the map's objects, at least 5 m and not flat, which build_map.py leaves out or lifts), preferring rotated ones and
every object with a pitch or roll (one per template and rotation). Vegetation is left out: the map draws stand-in
trees. Writes each object's BF2 position and rotation, its template GLB's vertices and
the map's object vertices around it, all as glTF/three.js coordinates exactly as GLTFLoader would see them (the
importer's Z-up conversion undone).
"""
import argparse
import json
import math
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# build_map.py's defaults: objects smaller than this are left out of the map; this LOD is used
MAP_MIN_SIZE, MAP_LOD = 5.0, 1


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--level", required=True)
    p.add_argument("--map", required=True)
    p.add_argument("--templates", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--count", type=int, default=12, help="Objects with only a yaw to check (all pitched/rolled ones are added)")
    return p.parse_args(argv)


def parse_static_objects(path):
    objects, current = [], None
    for line in open(path, encoding="latin-1"):
        parts = line.split()
        if not parts:
            continue
        if parts[0] == "Object.create":
            current = {"name": parts[1].lower(), "pos": None, "rot": [0.0, 0.0, 0.0]}
            objects.append(current)
        elif current is not None and parts[0] == "Object.absolutePosition":
            current["pos"] = [float(v) for v in parts[1].split("/")]
        elif current is not None and parts[0] == "Object.rotation":
            current["rot"] = [float(v) for v in parts[1].split("/")]
    return [o for o in objects if o["pos"] is not None]


def import_vertices(path, names=None):
    """World vertices of the GLB's meshes (optionally only those named), in glTF Y-up coordinates"""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    bpy.ops.import_scene.gltf(filepath=path)
    result = []
    for obj in bpy.data.objects:
        if obj.type != "MESH" or (names and not any(obj.name.startswith(n) for n in names)):
            continue
        co = np.empty(len(obj.data.vertices) * 3)
        obj.data.vertices.foreach_get("co", co)
        co = co.reshape(-1, 3)
        m = np.array(obj.matrix_world)
        co = co @ m[:3, :3].T + m[:3, 3]
        # The importer turned glTF (x, y, z) into Blender (x, -z, y)
        result.append(np.stack([co[:, 0], co[:, 2], -co[:, 1]], 1))
    return np.concatenate(result) if result else np.zeros((0, 3))


def main():
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    catalog = {e["name"]: e for e in json.load(open(os.path.join(args.templates, "catalog.json")))["templates"]}
    objects = parse_static_objects(args.level)

    def eligible(o):
        e = catalog.get(o["name"])
        # The map draws vegetation as simple stand-in trees
        if e is None or e.get("parts", 1) != 1 or e["lod"] != MAP_LOD or e["category"].startswith("vegitation"):
            return False
        size = np.array(e["max"]) - np.array(e["min"])
        return size.max() >= MAP_MIN_SIZE and size[1] >= 0.05

    candidates = [o for o in objects if eligible(o)]
    tilted, keys = [], set()
    for o in candidates:
        key = (o["name"], tuple(o["rot"]))
        if (abs(o["rot"][1]) > 0.05 or abs(o["rot"][2]) > 0.05) and key not in keys:
            tilted.append(o)
            keys.add(key)
    yawed, seen = [], {o["name"] for o in tilted}
    for o in sorted(candidates, key=lambda o: -abs(o["rot"][0])):
        if o not in tilted and o["rot"][0] % 90 != 0 and o["name"] not in seen:
            yawed.append(o)
            seen.add(o["name"])
    chosen = tilted + yawed[: args.count]
    print(f"VERIFY {len(objects)} objects, {len(candidates)} eligible, {len(tilted)} with pitch or roll, {len(chosen)} chosen")

    map_vertices = import_vertices(args.map, ["objects", "see_through"])
    print(f"VERIFY map object vertices {len(map_vertices)}")
    out = []
    for o in chosen:
        local = import_vertices(os.path.join(args.templates, f"{o['name']}.glb"))
        # The map's vertices near the object, whatever its rotation: within its radius of its position
        center = np.array([o["pos"][0], o["pos"][1], -o["pos"][2]])
        radius = np.linalg.norm(local, axis=1).max() + 2
        near = map_vertices[np.linalg.norm(map_vertices - center, axis=1) <= radius]
        out.append({
            "name": o["name"], "pos": o["pos"], "rot": o["rot"],
            "local": np.round(local, 5).ravel().tolist(), "map": np.round(near, 5).ravel().tolist(),
        })
        print(f"VERIFY {o['name']} rot {o['rot']} local {len(local)} map nearby {len(near)}")
    with open(args.out, "w") as f:
        json.dump(out, f)
    print(f"VERIFY wrote {args.out}")


main()
