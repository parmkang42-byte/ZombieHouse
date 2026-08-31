# -*- coding: utf-8 -*-
"""
Two trees: a bare forest trunk and a buttressed jungle trunk.

    blender --background --python Tools_Props/tree.py

Trees are the most numerous prop in the game by a wide margin — the wood grows a few hundred,
the valley a few hundred more — and every one is currently a slightly-rotated box with a
couple of foliage cubes floating above it. At the range you fight at, the *trunk silhouette*
is the whole tree: the canopy is decoration overhead and the roots are what you see when
something walks out from behind one.

What makes a trunk read as a trunk rather than as a post:

**It is not straight.** Real trunks lean, and they bend more than once. A pole with a texture
on it still reads as a pole; a pole with two lazy bends in it reads as grown.

**It is not round.** A cross-section that varies — wider one way, lumpy in places — catches
light unevenly down its length, and that variation is what the eye actually reads as bark.

**It flares at the bottom.** Every tree is wider where it meets the ground. This is the
single cheapest cue and the most consistently missing one, because a box has no bottom.

**It gets thinner as it rises**, and the taper is not linear.

The jungle version adds buttress roots, which is the shape that says *tropical* before
anything else in the frame does.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import clear_scene, noise3, finish

SEGMENTS = 12      # around the trunk
RINGS = 16         # up it


def build_trunk(name, seed, buttresses=0, lean=0.10, flare=1.35):
    """
    A lofted tube: a ring of vertices at each height, offset by a wandering centre line.

    The centre line is the important part. Sweeping it with two low-frequency sines gives a
    trunk that leans one way low down and corrects higher up, which is what a tree that grew
    towards light actually does — and it costs two sine calls per ring.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    grid = []

    for ring in range(RINGS + 1):
        t = ring / float(RINGS)
        z = t * 2.0 - 1.0

        # The wander. Two frequencies so it is not a simple arc.
        cx = math.sin(t * 2.1 + seed * 0.01) * lean * t
        cy = math.cos(t * 1.4 + seed * 0.02) * lean * 0.8 * t

        # Taper: fast near the ground, slow above. pow() rather than lerp, because a linear
        # taper reads as a machined cone.
        radius = 0.5 * (1.0 - 0.55 * math.pow(t, 0.75))

        # The flare. Only the bottom fifth, rising steeply into the ground.
        if t < 0.20:
            radius *= 1.0 + (flare - 1.0) * math.pow(1.0 - t / 0.20, 2.2)

        row = []
        for segment in range(SEGMENTS):
            angle = segment / float(SEGMENTS) * math.pi * 2.0

            # Out-of-round: a slow lobe around the circumference plus fine bark noise, both
            # varying with height so the shape twists rather than extruding one outline.
            lobe = math.sin(angle * 3.0 + t * 4.0 + seed) * 0.055
            bark = noise3(Vector((math.cos(angle), math.sin(angle), z * 3.0)), 3.0, seed) * 0.035

            r = radius * (1.0 + lobe) + bark

            row.append(bm.verts.new((math.cos(angle) * r + cx,
                                     math.sin(angle) * r + cy,
                                     z)))
        grid.append(row)

    bm.verts.ensure_lookup_table()

    for ring in range(RINGS):
        for segment in range(SEGMENTS):
            nxt = (segment + 1) % SEGMENTS
            bm.faces.new((grid[ring][segment], grid[ring][nxt],
                          grid[ring + 1][nxt], grid[ring + 1][segment]))

    bm.faces.new(list(reversed(grid[0])))
    bm.faces.new(grid[RINGS])

    # --- buttress roots -------------------------------------------------------
    # Fins that flare from the base and die away by a third of the way up. This is the
    # jungle's signature, and it is also the game's best cover — the shape has to read
    # clearly enough that a player recognises it as something to stand behind.
    for b in range(buttresses):
        angle = b / float(buttresses) * math.pi * 2.0 + seed * 0.01
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        across = Vector((-direction.y, direction.x, 0.0))

        fin = []
        FIN_RINGS = 6
        for ring in range(FIN_RINGS + 1):
            ft = ring / float(FIN_RINGS)
            z = -1.0 + ft * 0.62

            # Reach out furthest at the ground and taper to nothing as it climbs.
            reach = 0.62 * math.pow(1.0 - ft, 1.7)
            thickness = 0.075 * (1.0 - ft * 0.55)

            outer = direction * (0.30 + reach)
            fin.append((
                bm.verts.new(tuple(outer + across * thickness + Vector((0, 0, z)))),
                bm.verts.new(tuple(outer - across * thickness + Vector((0, 0, z)))),
                bm.verts.new(tuple(direction * 0.22 + across * thickness + Vector((0, 0, z)))),
                bm.verts.new(tuple(direction * 0.22 - across * thickness + Vector((0, 0, z)))),
            ))

        for ring in range(FIN_RINGS):
            a, b2, c, d = fin[ring]
            a2, b3, c2, d2 = fin[ring + 1]
            bm.faces.new((a, b2, b3, a2))     # outer edge
            bm.faces.new((c, d, d2, c2))      # inner edge
            bm.faces.new((a, c, c2, a2))      # one face
            bm.faces.new((b2, d, d2, b3))     # the other

        bm.faces.new(fin[0][:2] + fin[0][2:][::-1])

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    return obj


def main():
    # The wood: bare, leaning, flared. 380 triangles because there are hundreds on screen.
    clear_scene()
    finish(build_trunk("TreeTrunk", 3301, buttresses=0, lean=0.13, flare=1.38),
           "TreeTrunk", 380, lay_along="y")

    # The valley: shorter, fatter, and rooted. More budget — there are fewer of them and
    # the roots are the reason to have it at all.
    clear_scene()
    finish(build_trunk("JungleTrunk", 9109, buttresses=4, lean=0.07, flare=1.30),
           "JungleTrunk", 620, lay_along="y")


if __name__ == "__main__":
    main()
