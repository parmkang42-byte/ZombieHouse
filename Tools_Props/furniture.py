# -*- coding: utf-8 -*-
"""
Interior props: a school desk, a chandelier, and a stair flight.

    blender --background --python Tools_Props/furniture.py

These are the three the player gets closest to, and closeness is what decides whether a box
survives. A rock at fifteen metres in fog can be a lump; a desk you walk around at arm's
length cannot be a cube.

Each one is built the same way — a handful of bevelled parts welded into a single mesh —
because a bevelled edge catching a highlight is most of what separates furniture from
geometry, and because a prop must export as one object.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import block, clear_scene, finish, join


def cylinder(name, centre, radius, height, segments=10, axis="z"):
    """A tube. Chair legs, chandelier arms, stair balusters."""
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius, depth=height)

    if axis == "x":
        bmesh.ops.rotate(bm, verts=bm.verts,
                         matrix=__import__("mathutils").Matrix.Rotation(math.pi / 2, 3, "Y"))
    elif axis == "y":
        bmesh.ops.rotate(bm, verts=bm.verts,
                         matrix=__import__("mathutils").Matrix.Rotation(math.pi / 2, 3, "X"))

    for vert in bm.verts:
        vert.co += Vector(centre)

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


# ---------------------------------------------------------------------------------
def build_desk():
    """
    A school desk: sloped lid, a shelf under it, and a tubular steel frame.

    The slope is the whole silhouette. A flat-topped desk is a table; a lid tilted five
    degrees towards the chair is unmistakably a school desk, and it costs one rotation.
    """
    parts = []

    # The lid, tilted.
    lid = block("Lid", (0.0, 0.0, 0.34), (0.62, 0.44, 0.022), bevel=0.006, segments=2)
    bm = bmesh.new()
    bm.from_mesh(lid.data)
    for vert in bm.verts:
        # Pivot the tilt about the desk's centre so the back edge lifts and the front drops.
        vert.co.z += vert.co.y * 0.09
    bm.to_mesh(lid.data)
    bm.free()
    parts.append(lid)

    # A pencil groove along the high edge — the one detail everybody remembers.
    parts.append(block("Groove", (0.0, 0.185, 0.375), (0.5, 0.018, 0.012), bevel=0.004))

    # The shelf the books go on.
    parts.append(block("Shelf", (0.0, 0.0, 0.16), (0.55, 0.38, 0.016), bevel=0.005))

    # Tubular frame: four legs and two cross-rails, thin enough to read as steel.
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(cylinder("Leg_%d_%d" % (sx, sy),
                                  (0.27 * sx, 0.17 * sy, 0.0), 0.016, 0.70, segments=8))

    for sy in (-1, 1):
        parts.append(cylinder("Rail_%d" % sy, (0.0, 0.17 * sy, -0.28), 0.013, 0.56,
                              segments=8, axis="x"))

    return join(parts, "Desk")


# ---------------------------------------------------------------------------------
def build_chandelier():
    """
    A chandelier: a central column, a ring, and six arms carrying candle cups.

    The arms are what make it. A chandelier is a *radial* object — you read it from below,
    against a ceiling, as a wheel of little lights — so the arms and the ring matter far
    more than any amount of detail on the body, and the body is barely visible anyway.
    """
    parts = []

    # The stem, hanging from the ceiling.
    parts.append(cylinder("Chain", (0.0, 0.0, 0.62), 0.012, 0.76, segments=6))
    parts.append(cylinder("Column", (0.0, 0.0, 0.06), 0.045, 0.42, segments=10))
    parts.append(block("Boss", (0.0, 0.0, -0.13), (0.09, 0.09, 0.07), bevel=0.02, segments=2))

    ARMS = 6
    RING = 0.42

    # The ring, built as a chain of short segments rather than a torus: a hexagonal ring is
    # indistinguishable from a round one at ceiling height and costs a tenth as much.
    for i in range(ARMS):
        a0 = i / float(ARMS) * math.pi * 2.0
        a1 = (i + 1) / float(ARMS) * math.pi * 2.0

        p0 = Vector((math.cos(a0) * RING, math.sin(a0) * RING, -0.02))
        p1 = Vector((math.cos(a1) * RING, math.sin(a1) * RING, -0.02))
        mid = (p0 + p1) * 0.5

        seg = cylinder("Ring_%d" % i, tuple(mid), 0.014, (p1 - p0).length, segments=6, axis="x")
        seg.rotation_euler = (0.0, 0.0, math.atan2(p1.y - p0.y, p1.x - p0.x))
        parts.append(seg)

    for i in range(ARMS):
        angle = i / float(ARMS) * math.pi * 2.0
        out = Vector((math.cos(angle), math.sin(angle), 0.0))

        # An arm sweeping out and up from the boss to the ring.
        arm = cylinder("Arm_%d" % i, tuple(out * RING * 0.55 + Vector((0, 0, -0.07))),
                       0.012, RING * 1.15, segments=6, axis="x")
        arm.rotation_euler = (0.0, -0.36, angle)
        parts.append(arm)

        # The cup, and a stub of candle in it.
        parts.append(cylinder("Cup_%d" % i, tuple(out * RING + Vector((0, 0, 0.04))),
                              0.045, 0.05, segments=8))
        parts.append(cylinder("Candle_%d" % i, tuple(out * RING + Vector((0, 0, 0.12))),
                              0.022, 0.13, segments=6))

    return join(parts, "Chandelier")


# ---------------------------------------------------------------------------------
def build_stairs():
    """
    A stair flight: twelve treads with nosings, plus a stringer down each side.

    Stairs are currently a ramp, and a ramp is the one piece of level geometry a player
    reads as unfinished immediately — everyone has climbed stairs, so everyone knows what
    they should look like. The nosing (the tread overhanging the riser by a couple of
    centimetres) is the detail that sells it, and it is the one people leave out.
    """
    parts = []

    STEPS = 12
    rise = 1.0 / STEPS
    going = 1.0 / STEPS

    for step in range(STEPS):
        z = -0.5 + rise * (step + 0.5)
        y = -0.5 + going * (step + 0.5)

        # The tread, overhanging its riser at the front.
        parts.append(block("Tread_%d" % step,
                           (0.0, y - going * 0.06, z + rise * 0.42),
                           (0.5, going * 0.62, rise * 0.10),
                           bevel=0.004, segments=2))

        # The riser behind it.
        parts.append(block("Riser_%d" % step,
                           (0.0, y + going * 0.42, z),
                           (0.48, rise * 0.09, rise * 0.42),
                           bevel=0.003))

    # A stringer down each side, following the staircase as a sloped slab.
    for sx in (-1, 1):
        stringer = block("Stringer_%d" % sx, (0.5 * sx, 0.0, 0.0), (0.03, 0.72, 0.72),
                         bevel=0.006)
        bm = bmesh.new()
        bm.from_mesh(stringer.data)
        # Shear it into the diagonal the stairs actually run along.
        for vert in bm.verts:
            vert.co.z = vert.co.z * 0.30 + vert.co.y * 0.62
        bm.to_mesh(stringer.data)
        bm.free()
        parts.append(stringer)

    return join(parts, "Stairs")


def main():
    # bevelled=False throughout this file, and it is not an oversight.
    #
    # `block` already bevels every box it makes, which is the whole reason it exists -- these
    # props are built from right angles and a right angle is precisely what needs breaking.
    # _propkit's global bevel pass exists for the *organic* props (boulders, trunks, columns),
    # which are noise-displaced surfaces with no bevel anywhere.
    #
    # Running both is not just redundant, it is expensive: bevelling an already-bevelled edge
    # replaces one edge with three, and the staircase -- twelve treads and twelve risers, every
    # edge a right angle -- came out at 3,122 triangles against a 1,500 budget. If furniture
    # needs a stronger edge, widen the `bevel=` on the block that needs it, where the cost is
    # one box rather than the whole prop.
    clear_scene()
    finish(build_desk(), "Desk", 700, lay_along="y", bevelled=False)

    clear_scene()
    finish(build_chandelier(), "Chandelier", 900, lay_along="y", bevelled=False)

    clear_scene()
    # 700 rather than 900: the decimator counts polygons and the exporter triangulates
    # them, so the number Unity sees is roughly double. 900 came out at 1,700 triangles,
    # over the per-prop budget Test Props enforces.
    finish(build_stairs(), "Stairs", 700, lay_along="y", bevelled=False)


if __name__ == "__main__":
    main()
