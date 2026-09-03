# -*- coding: utf-8 -*-
"""
Four more props the player gets close to: a sarcophagus, a crate, a bed and a chair.

    blender --background --python Tools_Props/furnishings.py

Chosen by counting what the levels actually build out of raw boxes and picking the ones a
box serves worst. A crate is nearly a cube already, so it needs the least help and appears
the most; a sarcophagus is the opposite -- it appears a handful of times in one level, and a
box reads as a filing cabinet with a face drawn on it.

Every prop here is built the same way as the furniture: bevelled blocks welded into one
mesh, exported at a unit box. `bevelled=False` at the finish, because `block` already bevels
and running the global pass on top of that triples the triangle count for nothing.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Vector

from _propkit import block, clear_scene, finish, join


def taper(obj, axis, at_low, at_high, along="y"):
    """
    Squeeze a block's cross-section along one axis as a function of another.

    This is the whole reason a sarcophagus does not look like a box. An anthropoid coffin is
    wide at the shoulders and narrow at the feet, and that single change carries more of the
    read than any amount of surface detail — a straight-sided stone box is a trough.
    """
    mesh = obj.data
    lo = min(v.co[1 if along == "y" else 2] for v in mesh.vertices)
    hi = max(v.co[1 if along == "y" else 2] for v in mesh.vertices)
    span = max(1e-6, hi - lo)
    index = 1 if along == "y" else 2

    for vert in mesh.vertices:
        t = (vert.co[index] - lo) / span
        vert.co[axis] *= at_low + (at_high - at_low) * t


def build_sarcophagus():
    """
    A stone coffin: tapered chest, a lid standing proud of it, and a mask at the head.

    The lid is deliberately offset a few millimetres sideways and left slightly ajar. A
    perfectly seated lid reads as sealed and therefore as scenery; one that has shifted
    reads as something that has been opened, which is a different sentence entirely.
    """
    parts = []

    chest = block("Chest", (0.0, 0.0, -0.06), (0.30, 0.92, 0.30), bevel=0.016)
    taper(chest, 0, 0.72, 1.0)          # narrow at the feet, wide at the shoulders
    parts.append(chest)

    # A moulding running round the chest at shoulder height, where the taper turns over.
    parts.append(block("Shoulder", (0.0, 0.30, -0.06), (0.32, 0.05, 0.32), bevel=0.010))

    lid = block("Lid", (0.022, 0.0, 0.20), (0.29, 0.90, 0.10), bevel=0.014)
    taper(lid, 0, 0.72, 1.0)
    parts.append(lid)

    # The mask. Four blocks is enough at the distance anyone sees this from — the point is
    # a face-shaped interruption in the silhouette, not a portrait.
    parts.append(block("Face", (0.0, 0.52, 0.26), (0.17, 0.22, 0.07), bevel=0.012))
    parts.append(block("Headdress", (0.0, 0.66, 0.24), (0.24, 0.13, 0.09), bevel=0.014))
    parts.append(block("Beard", (0.0, 0.38, 0.27), (0.05, 0.12, 0.06), bevel=0.008))
    parts.append(block("Collar", (0.0, 0.27, 0.25), (0.22, 0.07, 0.07), bevel=0.010))

    return join(parts, "Sarcophagus")


def build_crate():
    """
    A packing crate: six panels, corner battens, and a plank line across each face.

    The battens are the whole prop. A crate without them is a cube; with them it is a
    made object, because the eye reads the corner posts as construction.
    """
    parts = []

    parts.append(block("Body", (0.0, 0.0, 0.0), (0.46, 0.46, 0.46), bevel=0.010))

    # Corner battens, running up all four vertical edges.
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(block("Batten_%d_%d" % (sx, sy),
                               (0.455 * sx, 0.455 * sy, 0.0),
                               (0.055, 0.055, 0.48), bevel=0.008))

    # A rail across the middle of each of the four sides, and one round the lid.
    for sx in (-1, 1):
        parts.append(block("RailX_%d" % sx, (0.47 * sx, 0.0, 0.0),
                           (0.035, 0.47, 0.06), bevel=0.007))
        parts.append(block("RailY_%d" % sx, (0.0, 0.47 * sx, 0.0),
                           (0.47, 0.035, 0.06), bevel=0.007))

    parts.append(block("LidRail", (0.0, 0.0, 0.47), (0.47, 0.47, 0.035), bevel=0.007))

    return join(parts, "Crate")


def build_bed():
    """
    An iron bedstead: head and foot rails with uprights, a sagging mattress, a pillow.

    The sag matters more than it sounds. A flat slab of mattress is the tell that something
    is geometry rather than an object — nothing that has been slept on stays flat, and a few
    centimetres of dip in the middle is the difference between a bed and a plinth.
    """
    parts = []

    # Frame rails at the perimeter.
    parts.append(block("RailL", (-0.44, 0.0, -0.10), (0.05, 0.98, 0.12), bevel=0.010))
    parts.append(block("RailR", (0.44, 0.0, -0.10), (0.05, 0.98, 0.12), bevel=0.010))

    for sy, name, height in ((-1, "Foot", 0.30), (1, "Head", 0.62)):
        parts.append(block(name + "Rail", (0.0, 0.47 * sy, height * 0.5 - 0.16),
                           (0.90, 0.05, height), bevel=0.012))
        for sx in (-1, 1):
            parts.append(block("%sPost_%d" % (name, sx),
                               (0.44 * sx, 0.47 * sy, height * 0.5 - 0.14),
                               (0.06, 0.06, height + 0.06), bevel=0.010))

    mattress = block("Mattress", (0.0, -0.02, 0.02), (0.86, 0.92, 0.15), bevel=0.030,
                     segments=2)

    # Dip it towards the middle. Cosine so the edges stay put and the centre drops.
    mesh = mattress.data
    for vert in mesh.vertices:
        if vert.co.z > 0.0:
            across = math.cos(vert.co.x / 0.86 * math.pi)
            along = math.cos(vert.co.y / 0.92 * math.pi)
            vert.co.z -= 0.045 * max(0.0, across) * max(0.0, along)
    parts.append(mattress)

    parts.append(block("Pillow", (0.0, 0.31, 0.10), (0.52, 0.20, 0.09), bevel=0.040,
                       segments=2))

    return join(parts, "Bed")


def build_chair():
    """
    A plain wooden chair: seat, four splayed legs, a back with three slats.

    Splayed legs are the detail worth the four extra lines. Vertical legs read as a stool
    with a board behind it; the outward lean is what the eye recognises as a chair.
    """
    parts = []

    parts.append(block("Seat", (0.0, 0.0, 0.02), (0.44, 0.42, 0.05), bevel=0.010))

    for sx in (-1, 1):
        for sy in (-1, 1):
            leg = block("Leg_%d_%d" % (sx, sy), (0.19 * sx, 0.18 * sy, -0.25),
                        (0.045, 0.045, 0.50), bevel=0.007)
            # Splay: push the foot further out than the top.
            for vert in leg.data.vertices:
                if vert.co.z < -0.25:
                    vert.co.x += 0.045 * sx
                    vert.co.y += 0.030 * sy
            parts.append(leg)

    # Back uprights carrying three horizontal slats.
    for sx in (-1, 1):
        parts.append(block("Upright_%d" % sx, (0.19 * sx, 0.19, 0.28),
                           (0.045, 0.045, 0.52), bevel=0.007))

    for i, z in enumerate((0.16, 0.32, 0.48)):
        parts.append(block("Slat_%d" % i, (0.0, 0.19, z), (0.40, 0.030, 0.075),
                           bevel=0.008))

    return join(parts, "Chair")


def main():
    clear_scene()
    finish(build_sarcophagus(), "Sarcophagus", 900, lay_along="y", bevelled=False)

    clear_scene()
    finish(build_crate(), "Crate", 700, lay_along="y", bevelled=False)

    clear_scene()
    finish(build_bed(), "Bed", 900, lay_along="y", bevelled=False)

    clear_scene()
    finish(build_chair(), "Chair", 700, lay_along="y", bevelled=False)


if __name__ == "__main__":
    main()
