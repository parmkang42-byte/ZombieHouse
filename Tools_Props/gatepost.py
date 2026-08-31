# -*- coding: utf-8 -*-
"""
A carved temple gate post: stacked stone blocks, weathered edges, a glyph band near the top.

    blender --background --python Tools_Props/gatepost.py

The jungle's gate is the last thing you see before the level ends, and it is currently two
1.1 x 4 x 1.1 boxes with a box across them. It is also the one prop in the valley the player
walks right up to, so it is the one where the difference between a box and a shape is most
visible.

Built as a stack of separate blocks rather than one carved column, because the courses are
the point — a gate post that is obviously *assembled* from stones reads as built-and-then-
abandoned, which is the whole story of the level's ruins.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import clear_scene, noise3, finish, join

NAME = "GatePost"
SEED = 7731

COURSES = 7            # stone blocks up the post
TARGET_TRIANGLES = 520


def weathered_block(name, centre, size, seed, chamfer=0.055):
    """
    One stone: a box with its corners knocked off and its faces pushed about slightly.

    The chamfer matters more than the noise. A sharp-edged box reads as new; a stone with
    softened arrises reads as old, and it costs eight vertices per corner to say so.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    bmesh.ops.create_cube(bm, size=1.0)

    for vert in bm.verts:
        vert.co.x *= size[0]
        vert.co.y *= size[1]
        vert.co.z *= size[2]

    # Bevel every edge once. This is the single most valuable operation Blender offers this
    # project: catching a highlight along a softened edge is most of what makes stone read
    # as stone, and it is not something a Unity primitive can be talked into.
    bmesh.ops.bevel(bm,
                    geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                    offset=chamfer,
                    segments=2,
                    affect="EDGES",
                    profile=0.5)

    # Then push the surface about so no two stones in the stack are identical.
    for vert in bm.verts:
        wobble = noise3(vert.co + Vector(centre), 3.1, seed) * 0.022
        vert.co += vert.co.normalized() * wobble

    for vert in bm.verts:
        vert.co += Vector(centre)

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def build_gatepost(seed):
    blocks = []

    height = 2.0
    course_height = height / COURSES

    for course in range(COURSES):
        z = -height * 0.5 + course_height * (course + 0.5)

        # Courses alternate slightly in depth, so the stack has a shadow line between
        # stones rather than presenting one flat face.
        jut = 0.02 if course % 2 == 0 else -0.01
        taper = 1.0 - (course / float(COURSES)) * 0.06

        size = (0.5 * taper + jut, 0.5 * taper + jut, course_height * 0.5 - 0.012)

        block = weathered_block("Course_%d" % course, (0.0, 0.0, z), size, seed + course * 17)
        blocks.append(block)

    # A glyph band: a shallow raised course near the top, the one piece of ornament. It is
    # the same trick as the hieroglyph bands in the tomb — a horizontal accent at eye height
    # tells you something built this on purpose.
    band_z = height * 0.5 - course_height * 1.5
    band = weathered_block("GlyphBand", (0.0, 0.0, band_z),
                           (0.55, 0.55, course_height * 0.22), seed + 401, chamfer=0.02)
    blocks.append(band)

    # A cap stone, wider than the shaft, which is what stops it looking like a cut-off pillar.
    cap = weathered_block("Cap", (0.0, 0.0, height * 0.5 + 0.04),
                          (0.58, 0.58, 0.075), seed + 909, chamfer=0.03)
    blocks.append(cap)

    return join(blocks, NAME)


def main():
    clear_scene()
    obj = build_gatepost(SEED)

    # Upright: the generator's gate posts are boxes tall in Y.
    finish(obj, NAME, TARGET_TRIANGLES, lay_along="y")


if __name__ == "__main__":
    main()
