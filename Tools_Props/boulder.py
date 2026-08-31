# -*- coding: utf-8 -*-
"""
The game's boulder: an icosphere pushed about by noise, flattened underneath, decimated.

    blender --background --python Tools_Props/boulder.py

Rocks are the worst of the programmer art. The wood scatters 66 of them, the thickets add 26
more, and the valley another 40 — and every one is a cube rotated by a random few degrees.
They are the first thing to fix because they are the most numerous and the most obviously
wrong.

Why Blender rather than more C#: the project already generates meshes in C# (the bear's fang,
the katana blade) and could generate a lump of rock too. What it cannot reasonably do is the
last step here — *decimate*. A displaced icosphere at full density is 5,120 triangles; the
collapse modifier takes it to 420 while keeping the silhouette, and at 132 rocks across three
levels that difference is the whole argument.

See _propkit for the unit-box contract that every prop honours.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import clear_scene, noise3, finish

NAME = "Boulder"
SEED = 20260827

# 5 subdivisions is 5,120 faces to carve. Enough detail that the noise has something to bite
# on, and the decimator throws away the excess afterwards.
SUBDIVISIONS = 5
TARGET_TRIANGLES = 420


def build_boulder(seed):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=SUBDIVISIONS, radius=1.0)
    obj = bpy.context.active_object
    obj.name = NAME

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)

    for vert in bm.verts:
        direction = vert.co.normalized()

        # Two scales of noise: big lobes that give the rock its shape, and a finer layer
        # that breaks up the surface so it does not read as a potato.
        lobes = noise3(vert.co, 1.1, seed)
        grain = noise3(vert.co, 4.3, seed + 91)

        vert.co = direction * (1.0 + lobes * 0.30 + grain * 0.09)

    # Rocks sit. Flat-ish underneath where one has settled into the ground, which reads
    # instantly and costs nothing. The overall squash is normalised away again — proportion
    # is the level generator's decision — but a flat underside is shape, and shape survives.
    for vert in bm.verts:
        vert.co.z *= 0.62
        if vert.co.z < -0.28:
            vert.co.z = -0.28 - (vert.co.z + 0.28) * 0.15

    bm.to_mesh(mesh)
    bm.free()

    return obj


def main():
    clear_scene()
    obj = build_boulder(SEED)

    # Default orientation: a rock has no long axis worth caring about.
    finish(obj, NAME, TARGET_TRIANGLES)


if __name__ == "__main__":
    main()
