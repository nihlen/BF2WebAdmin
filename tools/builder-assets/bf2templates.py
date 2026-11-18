"""
Reads the object templates of an extracted objects_server folder (the .con and .tweak files) and resolves each
template to the meshes the server would spawn for it: its own geometry plus the geometry of its child templates
(addTemplate) at their offsets, recursively.

Pure Python and numpy, so it runs in Blender and in a plain interpreter (python -I bf2templates.py <objects_server>
prints a summary).
"""
import math
import os
import sys

import numpy as np

# Deeper nesting than this is treated as a cycle
MAX_DEPTH = 8


def bf2_rotation_matrix(yaw, pitch, roll):
    """
    BF2 rotation (degrees, Object.rotation / ObjectTemplate.setRotation) -> 3x3 matrix in BF2 space, for column
    vectors (world = R @ local + position). Yaw 0 faces +Z (north), 90 faces +X (east). Same as BF2v2
    tools/replay-map/build_map.py
    """
    y, p, r = (math.radians(v) for v in (yaw, pitch, roll))
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    rz = np.array([[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]])
    return ry @ rx @ rz


def _vector(text):
    values = [float(v) for v in text.split("/")]
    return tuple((values + [0.0, 0.0, 0.0])[:3])


class Templates:
    """
    templates: lower name -> {name, type, geometry, part, children: [{name, pos, rot}], file}
    geometries: lower name -> {name, type, file}
    file is the defining .con/.tweak path relative to the objects_server root, with forward slashes
    """

    def __init__(self, server_root):
        self.root = server_root
        self.templates = {}
        self.geometries = {}
        self.duplicates = set()
        self._visited = set()
        files = []
        for dirpath, _, names in os.walk(server_root):
            for n in names:
                if n.lower().endswith((".con", ".tweak")):
                    files.append(os.path.join(dirpath, n))
        files.sort()
        # The .con files first, each with the .tweak it includes right where the include is, like the engine runs
        # them. Then any .tweak no .con included
        for path in files:
            if path.lower().endswith(".con"):
                self._run(path)
        for path in files:
            if path.lower().endswith(".tweak"):
                self._run(path)

    def _rel(self, path):
        return os.path.relpath(path, self.root).replace("\\", "/")

    def _run(self, path):
        key = os.path.normcase(os.path.abspath(path))
        if key in self._visited:
            return
        self._visited.add(key)
        rel = self._rel(path)
        current, child = None, None
        for line in open(path, encoding="latin-1"):
            parts = line.split()
            if not parts:
                continue
            cmd = parts[0].lower()
            if cmd == "include" and len(parts) >= 2:
                target = _find_case_insensitive(os.path.dirname(path), parts[1].replace("\\", "/"))
                if target:
                    self._run(target)
                continue
            if len(parts) < 2:
                continue
            if cmd == "geometrytemplate.create" and len(parts) >= 3:
                name = parts[2].lower()
                self.geometries.setdefault(name, {"name": parts[2], "type": parts[1].lower(), "file": rel})
            elif cmd in ("objecttemplate.create", "objecttemplate.activesafe") and len(parts) >= 3:
                current = parts[2].lower()
                child = None
                if cmd == "objecttemplate.create" and current in self.templates and self.templates[current]["created"]:
                    if self.templates[current]["file"].rsplit("/", 1)[0] != rel.rsplit("/", 1)[0]:
                        self.duplicates.add(current)
                    current = None  # the first definition wins
                    continue
                t = self.templates.setdefault(current, {
                    "name": parts[2], "type": parts[1].lower(), "geometry": None, "part": None, "children": [],
                    "file": rel, "created": False,
                })
                if cmd == "objecttemplate.create":
                    t.update(name=parts[2], type=parts[1].lower(), file=rel, created=True)
            elif current is None:
                continue
            elif cmd == "objecttemplate.geometry":
                self.templates[current]["geometry"] = parts[1].lower()
            elif cmd == "objecttemplate.geometrypart":
                self.templates[current]["part"] = int(parts[1])
            elif cmd == "objecttemplate.addtemplate":
                child = {"name": parts[1].lower(), "pos": (0.0, 0.0, 0.0), "rot": (0.0, 0.0, 0.0)}
                self.templates[current]["children"].append(child)
            elif cmd == "objecttemplate.setposition" and child is not None:
                child["pos"] = _vector(parts[1])
            elif cmd == "objecttemplate.setrotation" and child is not None:
                child["rot"] = _vector(parts[1])

    def category(self, name):
        """The template's folder: the first two folders of its .con file, e.g. staticobjects/_asia, roads/statics"""
        folders = self.templates[name]["file"].lower().split("/")[:-1]
        return "/".join(folders[:2])

    def pieces(self, name, mesh_index, rotation=None, offset=None, depth=0, missing=None):
        """
        The meshes the template spawns, in its own frame: [{kind: static|bundled, path, parts (bundled: {part:
        (R, p)}), rotation 3x3, offset 3, template}]. A piece's vertex v ends up at rotation @ v + offset.
        missing collects geometry names whose mesh file is not in mesh_index
        """
        rotation = np.eye(3) if rotation is None else rotation
        offset = np.zeros(3) if offset is None else offset
        t = self.templates.get(name)
        if t is None or depth > MAX_DEPTH:
            return []
        result = []
        geometry = self.geometries.get(t["geometry"]) if t["geometry"] else None
        if geometry is not None and geometry["type"] in ("staticmesh", "bundledmesh"):
            path = mesh_index.find(geometry, t["geometry"])
            if path is None:
                if missing is not None:
                    missing.add(t["geometry"])
            else:
                piece = {"kind": geometry["type"][:-4], "path": path, "rotation": rotation, "offset": offset, "template": name}
                if geometry["type"] == "bundledmesh":
                    piece["parts"] = self._part_transforms(t, depth)
                result.append(piece)
        elif geometry is not None and missing is not None and geometry["type"] not in ("staticmesh", "bundledmesh"):
            missing.add(f"{t['geometry']} ({geometry['type']})")
        for c in t["children"]:
            ct = self.templates.get(c["name"])
            if ct is None or (ct["geometry"] is None and ct["part"] is not None and not ct["children"]):
                continue  # unknown, or a part of this template's bundled mesh (see _part_transforms)
            r = bf2_rotation_matrix(*c["rot"])
            result += self.pieces(c["name"], mesh_index, rotation @ r, rotation @ np.array(c["pos"]) + offset, depth + 1, missing)
        return result

    def _part_transforms(self, t, depth, rotation=None, offset=None, result=None):
        """Bundled mesh parts are positioned by the child templates that name them (geometryPart), at the child's
        offset in the template tree. Part 0 and parts no child names stay at the origin"""
        rotation = np.eye(3) if rotation is None else rotation
        offset = np.zeros(3) if offset is None else offset
        result = {} if result is None else result
        if depth > MAX_DEPTH:
            return result
        for c in t["children"]:
            ct = self.templates.get(c["name"])
            if ct is None or ct["geometry"] is not None:
                continue
            r = rotation @ bf2_rotation_matrix(*c["rot"])
            p = rotation @ np.array(c["pos"]) + offset
            if ct["part"] is not None:
                result.setdefault(ct["part"], (r, p))
            self._part_transforms(ct, depth + 1, r, p, result)
        return result


class MeshIndex:
    """Finds a geometry's mesh file under objects_client: <folder of its .con>/meshes/<name>.<type>, else by name"""

    def __init__(self, client_root):
        self.root = client_root
        self.by_name = {}
        self.by_path = {}
        for dirpath, _, names in os.walk(client_root):
            for n in names:
                low = n.lower()
                if low.endswith((".staticmesh", ".bundledmesh")):
                    path = os.path.join(dirpath, n)
                    self.by_name.setdefault(low, path)
                    self.by_path[os.path.relpath(path, client_root).replace("\\", "/").lower()] = path

    def find(self, geometry, name):
        file_name = f"{name}.{geometry['type']}"
        folder = geometry["file"].lower().rsplit("/", 1)[0]
        return self.by_path.get(f"{folder}/meshes/{file_name}") or self.by_name.get(file_name)


def _find_case_insensitive(folder, relative):
    path = os.path.join(folder, relative)
    if os.path.exists(path):
        return path
    target = os.path.basename(relative).lower()
    parent = os.path.dirname(path)
    if os.path.isdir(parent):
        for n in os.listdir(parent):
            if n.lower() == target:
                return os.path.join(parent, n)
    return None


if __name__ == "__main__":
    server = sys.argv[1]
    t = Templates(server)
    print(f"{len(t.templates)} templates, {len(t.geometries)} geometries, {len(t.duplicates)} defined in more than one folder")
    if len(sys.argv) > 2:
        index = MeshIndex(sys.argv[2])
        for name in sys.argv[3:]:
            missing = set()
            for p in t.pieces(name.lower(), index, missing=missing):
                print(name, p["kind"], os.path.basename(p["path"]), np.round(p["offset"], 3).tolist(), p.get("parts"))
            print(name, "missing", missing, "category", t.category(name.lower()))
