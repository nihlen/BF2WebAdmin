"""
Builds the 3D viewer's helicopter hitboxes (src/BF2WebAdmin.Web/public/viewer/models/heli-hitboxes.json) from the
server's collision meshes: what a TV missile is tested against, not the visual model (helis.glb, BF2v2
tools/replay-map/build_helis.py).

    python -I tools/viewer/build_heli_hitboxes.py --objects <objects_server root> \
        --out src/BF2WebAdmin.Web/public/viewer/models/heli-hitboxes.json

--objects is an extracted objects_server folder with Vehicles/Air/<heli> and
Weapons/Armament/missiles/agm114_hellfire (the Hellfire dummies on the AH-1Z's rails use its mesh); for example the
netsky server's mods/bf2/objects_server.zip (byte-identical for the two helis to bf2-physics-clean
inputs/objects_server).

What the engine tests: a TV missile (physicsType Point) sweeps one point against each object's collision LOD 0
(coltype 0, the projectile layer; OpenBF2 Code/BF2/Physics/PointResponsePhysics.cpp:112-145). Every object with
hasCollisionPhysics 1 is tested on its own: the hull (collision part 0 of the vehicle's mesh, glass included) and the
child templates with a collisionPart in the vehicle's mesh or a collision mesh of their own (rotors, the Hellfire
dummies). Only parentless objects (the hull) and soldiers are tested at the shooter's rewound pose; child parts are
tested where they are now.

Output, all in BF2 coordinates (x right, y up, z forward; the viewer mirrors z):
    meshes:   name -> {v: [x, y, z, ...] in the part's own frame, i: [a, b, c, ...]}
    vehicles: template -> {parts: [{name, part, mesh, rewound, pos, rot, rotor?}]}
        pos, rot: the part's rest transform relative to the vehicle (rot a row-major 3x3 matrix), following the
        template tree with child rotations at rest (turret aim, spring travel and blade angle are not known)
        rotor: {axis 0/1/2, radius, min, max}: the spin axis in the part's frame, the blades' reach and their extent
        along the axis, to draw the volume the blades sweep
"""
import argparse
import json
import math
import os
import struct

HELIS = ["ahe_ah1z", "ahe_z10"]


def read_collisionmesh(data):
    """
    The projectile layer (coltype 0) of each geometry part's first sub-geometry: {part: (vertices, faces)}. Layout as
    bf2-bots bf2colmesh.py. A vehicle's hull has a second sub-geometry whose faces are all the "wreck" material (AH-1Z
    material 4, Z-10 material 2): the wreck's, not the flying helicopter's, so it is left out
    """
    pos = 0

    def take(fmt):
        nonlocal pos
        v = struct.unpack_from("<" + fmt, data, pos)
        pos += struct.calcsize("<" + fmt)
        return v

    _, version = take("2i")
    if version not in (8, 9, 10):
        raise ValueError(f"collisionmesh version {version}")
    parts = {}
    (geomnum,) = take("i")
    for g in range(geomnum):
        (subnum,) = take("i")
        for _ in range(subnum):
            (lodnum,) = take("i")
            for _ in range(lodnum):
                coltype = take("i")[0] if version >= 9 else 0
                (facenum,) = take("i")
                faces = [struct.unpack_from("<4H", data, pos + 8 * k)[:3] for k in range(facenum)]
                pos += facenum * 8
                (vertnum,) = take("i")
                verts = [struct.unpack_from("<3f", data, pos + 12 * k) for k in range(vertnum)]
                pos += vertnum * 12 + vertnum * 2 + 24 + 1 + 24
                (ynum,) = take("i")
                pos += ynum * 16
                (znum,) = take("i")
                pos += znum * 2
                if version >= 10:
                    (anum,) = take("i")
                    pos += anum * 4
                if coltype != 0 or g in parts:
                    continue
                parts[g] = (verts, faces)
    if pos != len(data):
        raise ValueError(f"parsed {pos} of {len(data)} bytes")
    return parts


def read_templates(root):
    """ObjectTemplate statements from every .con/.tweak under root, by lower-case name"""
    files = []
    for d, _, names in os.walk(root):
        files += [os.path.join(d, n) for n in names if n.lower().endswith((".con", ".tweak"))]
    templates = {}
    for path in sorted(files, key=lambda f: (f.lower().endswith(".tweak"), f.lower())):
        current = child = None
        for line in open(path, encoding="latin-1"):
            parts = line.split()
            if len(parts) < 2:
                continue
            cmd = parts[0].lower()
            if cmd in ("objecttemplate.create", "objecttemplate.activesafe", "objecttemplate.active"):
                current = parts[2] if len(parts) >= 3 else parts[1]
                templates.setdefault(current.lower(), {"name": current, "children": [], "collision": False, "part": None, "mesh": None})
                child = None
            elif current is None:
                continue
            else:
                t = templates[current.lower()]
                if cmd == "objecttemplate.collisionpart":
                    t["part"] = int(parts[1])
                elif cmd in ("objecttemplate.collisionmesh", "objecttemplate.setcollisionmesh"):
                    t["mesh"] = parts[1].lower()
                elif cmd == "objecttemplate.hascollisionphysics":
                    t["collision"] = parts[1] == "1"
                elif cmd == "objecttemplate.addtemplate":
                    child = {"name": parts[1].lower(), "pos": (0.0, 0.0, 0.0), "rot": (0.0, 0.0, 0.0)}
                    t["children"].append(child)
                elif cmd == "objecttemplate.setposition" and child is not None:
                    child["pos"] = tuple(float(v) for v in parts[1].split("/"))
                elif cmd == "objecttemplate.setrotation" and child is not None:
                    child["rot"] = tuple(float(v) for v in parts[1].split("/"))
    return templates


def dedupe_children(templates):
    """
    A template defined in both the .con and the .tweak lists its children twice: keep each (name, pos, rot) once. The
    8 Hellfire dummies differ in position, so they stay
    """
    for t in templates.values():
        seen = set()
        children = []
        for c in t["children"]:
            key = (c["name"], c["pos"], c["rot"])
            if key not in seen:
                seen.add(key)
                children.append(c)
        t["children"] = children


def rotation(yaw, pitch, roll):
    """BF2 rotation in degrees (yaw 0 = +z, positive pitch = nose down) as a 3x3 matrix, as BF2v2 build_helis.py"""
    y, p, r = (math.radians(v) for v in (yaw, pitch, roll))
    ry = [[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]]
    rx = [[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]]
    rz = [[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]]
    return mul(mul(ry, rx), rz)


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def apply(m, v):
    return [sum(m[i][k] * v[k] for k in range(3)) for i in range(3)]


def find_meshes(root):
    found = {}
    for d, _, names in os.walk(root):
        for n in names:
            if n.lower().endswith(".collisionmesh"):
                found[n[: -len(".collisionmesh")].lower()] = os.path.join(d, n)
    return found


def rnd(values, digits=3):
    return [round(v, digits) + 0.0 for v in values]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--objects", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()

    templates = read_templates(args.objects)
    dedupe_children(templates)
    mesh_files = find_meshes(args.objects)
    mesh_cache = {}

    def mesh(name):
        if name not in mesh_cache:
            mesh_cache[name] = read_collisionmesh(open(mesh_files[name], "rb").read())
        return mesh_cache[name]

    out_meshes = {}
    out_vehicles = {}

    def add_mesh(key, verts, faces):
        if key not in out_meshes:
            out_meshes[key] = {"v": rnd([c for p in verts for c in p]), "i": [k for f in faces for k in f]}

    for heli in HELIS:
        root = templates[heli]
        vehicle_mesh = mesh(root["mesh"])
        parts = []

        def add_part(t, part, mesh_key, verts, faces, rot, pos, rewound):
            add_mesh(mesh_key, verts, faces)
            entry = {
                "name": t["name"],
                "part": part,
                "mesh": mesh_key,
                "rewound": rewound,
                "pos": rnd(pos, 4),
                "rot": rnd([c for row in rot for c in row], 5),
            }
            if t["name"].lower().endswith(("mainrotor", "tailrotor")):
                # The spin axis: the flattest extent of the blades
                ext = [max(p[a] for p in verts) - min(p[a] for p in verts) for a in range(3)]
                axis = ext.index(min(ext))
                others = [a for a in range(3) if a != axis]
                entry["rotor"] = {
                    "axis": axis,
                    "radius": round(max(math.hypot(p[others[0]], p[others[1]]) for p in verts), 3),
                    "min": round(min(p[axis] for p in verts), 3),
                    "max": round(max(p[axis] for p in verts), 3),
                }
            parts.append(entry)
            print(f"  {t['name']:28} part {part!s:4} rewound {rewound!s:5} pos {rnd(pos, 2)} faces {len(faces)}")

        print(heli)
        hull = vehicle_mesh[0]
        add_part(root, 0, f"{heli}#0", *hull, [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [0, 0, 0], True)

        def visit(name, rot, pos, depth):
            t = templates.get(name)
            if t is None or depth > 20:
                return
            if depth > 0 and t["collision"]:
                if t["part"] is not None and t["part"] in vehicle_mesh:
                    add_part(t, t["part"], f"{heli}#{t['part']}", *vehicle_mesh[t["part"]], rot, pos, False)
                elif t["mesh"] and t["mesh"] in mesh_files and 0 in mesh(t["mesh"]):
                    add_part(t, None, t["mesh"], *mesh(t["mesh"])[0], rot, pos, False)
            for c in t["children"]:
                visit(c["name"], mul(rot, rotation(*c["rot"])), [a + b for a, b in zip(pos, apply(rot, c["pos"]))], depth + 1)

        visit(heli, [[1, 0, 0], [0, 1, 0], [0, 0, 1]], [0, 0, 0], 0)
        out_vehicles[heli] = {"parts": parts}

    doc = {
        "about": "TV-missile hitboxes (collision LOD 0) of the helicopters; tools/viewer/build_heli_hitboxes.py",
        "meshes": out_meshes,
        "vehicles": out_vehicles,
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(doc, f, separators=(",", ":"))
    print(f"wrote {args.out} ({os.path.getsize(args.out) / 1024:.0f} KB)")


main()
