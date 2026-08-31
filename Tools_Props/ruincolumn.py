# -*- coding: utf-8 -*-
"""
A toppled temple column: fluted, tapered, and broken off at one end.

    blender --background --python Tools_Props/ruincolumn.py

The jungle's ruins and the pyramid both place these as plain long cubes. A column is one of
the few props where the *silhouette* carries all the information — flutes down the side and a
jagged break at the end read as "ruin" from thirty metres in fog, which is exactly the range
the valley fights at, and no amount of texturing a box achieves the same thing.

The break is the point. A column with two clean ends is a pillar someone put down; a column
with one shattered end fell.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import clear_scene, noise3, finish

NAME = "RuinColumn"
SEED = 44117

SEGMENTS = 20        # around the shaft
RINGS = 14           # along it
FLUTES = 12          # the vertical grooves
TARGET_TRIANGLES = 460


def build_column(seed):
    """A cylinder with grooves cut into it by radius, tapered, then broken at the top."""
    mesh = bpy.data.meshes.new(NAME)
    bm = bmesh.new()

    grid = []

    for ring in range(RINGS + 1):
        t = ring / float(RINGS)
        z = t * 2.0 - 1.0

        # Classical columns taper. Only slightly — enough that the eye reads one end as
        # the base without the thing looking like a traffic cone.
        taper = 1.0 - t * 0.12

        # The break: above 70% of the height the radius collapses unevenly, so the top is
        # a ragged stump rather than a flat disc.
        broken = 0.0
        if t > 0.70:
            broken = (t - 0.70) / 0.30

        row = []
        for segment in range(SEGMENTS):
            angle = segment / float(SEGMENTS) * math.pi * 2.0

            # Flutes: a scalloped radius. Shallow, or they read as a gear.
            flute = math.cos(angle * FLUTES) * 0.035

            radius = (0.5 + flute) * taper

            if broken > 0.0:
                # Chew it away unevenly around the circumference so the break is jagged.
                bite = 0.5 + 0.5 * math.sin(angle * 3.0 + seed * 0.01)
                radius *= max(0.05, 1.0 - broken * (0.55 + bite * 0.75))

            wobble = noise3(Vector((math.cos(angle), math.sin(angle), z)), 2.2, seed) * 0.02
            radius += wobble

            row.append(bm.verts.new((math.cos(angle) * radius,
                                     math.sin(angle) * radius,
                                     z)))
        grid.append(row)

    bm.verts.ensure_lookup_table()

    for ring in range(RINGS):
        for segment in range(SEGMENTS):
            nxt = (segment + 1) % SEGMENTS
            bm.faces.new((grid[ring][segment], grid[ring][nxt],
                          grid[ring + 1][nxt], grid[ring + 1][segment]))

    # Cap the base flat — it was cut, not broken. The top is left open on purpose: the
    # break already closes to almost nothing, and a cap there would read as a lid.
    bm.faces.new(list(reversed(grid[0])))
    bm.faces.new(grid[RINGS])

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    return obj


def main():
    clear_scene()
    obj = build_column(SEED)
    # Lying down: the jungle and the pyramid both place these as boxes long in Z.
    finish(obj, NAME, TARGET_TRIANGLES, lay_along="z")


if __name__ == "__main__":
    main()
