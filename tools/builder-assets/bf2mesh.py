"""
Minimal readers for BF2 .bundledmesh (versions 6 and 10) and .staticmesh files (versions 4, 10, 11). Copied from
BF2v2 tools/replay-map/bf2mesh.py; read_bundledmesh returns numpy arrays here.

Layout: header, geometries (each with LODs), one shared vertex buffer and index buffer, per-LOD bounds and part
counts, then per-LOD materials with their vertex/index ranges. Vertices of a part are stored relative to the
part's pivot; the template tree positions the parts (see bf2templates.py).
"""
import struct

import numpy as np


class _Reader:
    def __init__(self, data):
        self.d, self.o = data, 0

    def rd(self, fmt):
        v = struct.unpack_from("<" + fmt, self.d, self.o)
        self.o += struct.calcsize("<" + fmt)
        return v

    def u32(self):
        return self.rd("I")[0]

    def string(self):
        n = self.u32()
        s = self.d[self.o:self.o + n].decode("latin-1")
        self.o += n
        return s


# D3DDECLUSAGE values used for the vertex attributes
USAGE_POSITION, USAGE_BLENDINDICES, USAGE_NORMAL, USAGE_TEXCOORD = 0, 2, 3, 5


def read_bundledmesh(path):
    """
    Returns {"geoms": LOD count per geometry, "lods": [{"geom", "lod", "min", "max", "parts", "materials"}],
    "positions": Nx3, "uvs": [Nx2 per texture coordinate set], "part": N part index per vertex (BLENDINDICES),
    "indices": the index buffer}. Versions 6 (a pivot after each LOD's bounds) and 10. Each part's vertices are
    relative to the part's pivot, which the template tree positions (ObjectTemplate.geometryPart)
    """
    r = _Reader(open(path, "rb").read())
    version = r.rd("5I")[1]
    r.rd("B")
    geoms = [r.u32() for _ in range(r.u32())]  # LOD count per geometry
    attribs = [r.rd("4H") for _ in range(r.u32())]  # flag, offset, type, usage
    _, stride, vnum = r.rd("3I")
    vdata = np.frombuffer(r.d, np.uint8, stride * vnum, r.o).reshape(vnum, stride)
    r.o += stride * vnum
    inum = r.u32()
    indices = np.frombuffer(r.d, "<u2", inum, r.o).astype(np.int64)
    r.o += inum * 2
    r.u32()

    lods = []
    for g, lodnum in enumerate(geoms):
        for l in range(lodnum):
            bmin, bmax = r.rd("3f"), r.rd("3f")
            if version <= 6:
                r.rd("3f")  # pivot
            lods.append({"geom": g, "lod": l, "min": bmin, "max": bmax, "parts": r.u32(), "materials": []})
    for lod in lods:
        for _ in range(r.u32()):
            alpha = r.u32()
            r.string()  # effect file
            technique = r.string()
            maps = [r.string() for _ in range(r.u32())]
            vstart, istart, count, vcount = r.rd("4I")
            r.rd("2I")
            lod["materials"].append(
                {"alpha": alpha, "technique": technique, "maps": maps, "vstart": vstart, "istart": istart, "inum": count, "vnum": vcount}
            )
    if r.o != len(r.d):
        raise ValueError(f"{path}: parsed {r.o} of {len(r.d)} bytes, unexpected layout")

    def floats(offset, n):
        return vdata[:, offset:offset + 4 * n].copy().view("<f4")

    attr = {usage: offset for flag, offset, _, usage in attribs if flag != 255}
    uvs = []
    while USAGE_TEXCOORD | len(uvs) << 8 in attr:
        uvs.append(floats(attr[USAGE_TEXCOORD | len(uvs) << 8], 2))
    part = vdata[:, attr[USAGE_BLENDINDICES]].astype(np.int64) if USAGE_BLENDINDICES in attr else np.zeros(vnum, np.int64)
    return {"geoms": geoms, "lods": lods, "positions": floats(attr[USAGE_POSITION], 3), "uvs": uvs, "part": part, "indices": indices}


def read_staticmesh(path):
    """
    Same layout as a bundledmesh, except each LOD has node matrices (and a pivot before version 7) and version 11
    adds bounds to each material. Static meshes are a single part with their vertices in the object's local
    BF2 space, usually centered on the object's position.

    Returns {"geoms": LOD count per geometry, "lods": [{"geom", "lod", "materials"}], "positions": Nx3,
    "uvs": [Nx2 per texture coordinate set], "indices": the index buffer}. A material's triangles are
    indices[istart:istart + inum] + vstart, in D3D (clockwise) winding
    """
    r = _Reader(open(path, "rb").read())
    version = r.rd("5I")[1]
    r.rd("B")
    geoms = [r.u32() for _ in range(r.u32())]
    attribs = [r.rd("4H") for _ in range(r.u32())]  # flag, offset, type, usage
    _, stride, vnum = r.rd("3I")
    vdata = np.frombuffer(r.d, np.uint8, stride * vnum, r.o).reshape(vnum, stride)
    r.o += stride * vnum
    inum = r.u32()
    indices = np.frombuffer(r.d, "<u2", inum, r.o).astype(np.int64)
    r.o += inum * 2
    r.u32()

    lods = []
    for g, lodnum in enumerate(geoms):
        for l in range(lodnum):
            r.rd("6f")  # bounds
            if version <= 6:
                r.rd("3f")  # pivot
            nodes = r.u32()
            r.o += 64 * nodes  # node matrices
            lods.append({"geom": g, "lod": l, "materials": []})
    for lod in lods:
        for _ in range(r.u32()):
            alpha = r.u32()
            r.string()  # effect file
            technique = r.string()
            maps = [r.string() for _ in range(r.u32())]
            vstart, istart, count, vcount = r.rd("4I")
            r.rd("2I")
            if version == 11:
                r.rd("6f")  # bounds
            lod["materials"].append(
                {"alpha": alpha, "technique": technique, "maps": maps, "vstart": vstart, "istart": istart, "inum": count, "vnum": vcount}
            )
    if r.o != len(r.d):
        raise ValueError(f"{path}: parsed {r.o} of {len(r.d)} bytes, unexpected layout")

    def floats(offset, n):
        return vdata[:, offset:offset + 4 * n].copy().view("<f4")

    attr = {usage: offset for flag, offset, _, usage in attribs if flag != 255}
    # Texture coordinate set k has usage TEXCOORD | k << 8
    uvs = []
    while USAGE_TEXCOORD | len(uvs) << 8 in attr:
        uvs.append(floats(attr[USAGE_TEXCOORD | len(uvs) << 8], 2))
    return {"geoms": geoms, "lods": lods, "positions": floats(attr[USAGE_POSITION], 3), "uvs": uvs, "indices": indices}
