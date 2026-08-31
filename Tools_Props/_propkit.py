# -*- coding: utf-8 -*-
"""
Shared scaffolding for the prop generators.

Every generator does the same four things — clear the scene, build something, normalise it
to a unit box, export it as OBJ — and only the middle step differs. That is worth factoring
out once rather than copy-pasting per prop, because the two boring steps are the ones with
the contract in them, and a prop that quietly skips normalisation arrives in the game at
whatever size Blender happened to leave it.

Import it from a generator sitting alongside:

    import sys, os
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from _propkit import clear_scene, noise3, finish
"""

import math
import os

import bpy
from mathutils import Vector

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(PROJECT, "Assets", "Resources", "Props")


def clear_scene():
    """Blender opens with a cube, a camera and a light. None of them are wanted."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def noise3(position, frequency, seed, octaves=4):
    """
    Layered value noise, written out rather than taken from mathutils.noise so a Blender
    upgrade cannot silently reshape every rock in the game.
    """
    total = 0.0
    amplitude = 1.0
    normaliser = 0.0

    for octave in range(octaves):
        f = frequency * (2.0 ** octave)

        value = (
            math.sin(position.x * f + seed * 0.017 + octave * 1.7)
            * math.sin(position.y * f * 1.13 + seed * 0.023 + octave * 2.3)
            * math.sin(position.z * f * 0.87 + seed * 0.031 + octave * 3.1)
        )

        total += value * amplitude
        normaliser += amplitude
        amplitude *= 0.5

    return total / normaliser


def join(objects, name):
    """Welds several objects into one, because a prop must export as a single mesh."""
    bpy.ops.object.select_all(action="DESELECT")

    for obj in objects:
        obj.select_set(True)

    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    merged = bpy.context.active_object
    merged.name = name
    return merged


def decimate(obj, target_triangles):
    """Collapse to a triangle budget. The silhouette survives; the count does not."""
    before = len(obj.data.polygons)
    if before <= target_triangles:
        return before, before

    modifier = obj.modifiers.new(name="Decimate", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = float(target_triangles) / float(before)

    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)

    return before, len(obj.data.polygons)


def normalise_to_unit_box(obj):
    """
    THE CONTRACT: a 1x1x1 bounding box centred on the origin.

    The level generators size every prop through localScale on a 1x1x1 cube, so a unit mesh
    drops in with no scale maths anywhere and the proportions stay the generator's decision.
    Note that this scales each axis independently — that is deliberate, not a bug. A rock
    modelled squat still arrives squat only if the *generator* asks for squat; what the
    model contributes is shape, not proportion.

    Applied to the mesh data rather than to object scale, because object scale does not
    reliably survive an OBJ export and relying on it is how a prop ends up double-size.
    """
    mesh = obj.data

    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)

    for vert in mesh.vertices:
        for axis in range(3):
            lo[axis] = min(lo[axis], vert.co[axis])
            hi[axis] = max(hi[axis], vert.co[axis])

    size = hi - lo
    centre = (hi + lo) * 0.5

    for axis in range(3):
        if size[axis] < 1e-6:
            size[axis] = 1.0

    for vert in mesh.vertices:
        for axis in range(3):
            vert.co[axis] = (vert.co[axis] - centre[axis]) / size[axis]

    return size


# Measured, not assumed: a lopsided box built 0.5 x 1.0 x 1.5 in Blender exports as
# 0.5 x 1.5 x 1.0. So Blender Z (up) becomes Unity Y (up), and Blender Y becomes Unity Z.
# Worth stating once here, because getting it wrong does not produce an obviously broken
# prop — it produces one stretched along the wrong axis, which reads as bad modelling.
LONG_AXIS_ROTATION = {
    "y": (0.0, 0.0, 0.0),                 # Blender Z -> Unity Y. Upright things.
    "z": (-math.pi / 2.0, 0.0, 0.0),      # Blender Z -> Unity Z. Things that lie down.
    "x": (0.0, math.pi / 2.0, 0.0),       # Blender Z -> Unity X.
}


def finish(obj, name, target_triangles, flat=True, lay_along="y"):
    """
    Decimate, orient, shade, normalise, export, then read the file back and check it.

    `lay_along` names the Unity axis the prop's long dimension should end up on, and it has
    to match whichever axis the level generator scales up. The jungle's toppled columns are
    boxes long in Z, so their mesh must be long in Z too; a column exported upright would be
    squashed flat and stretched sideways, which looks like bad modelling rather than like the
    axis mistake it is.
    """
    if lay_along not in LONG_AXIS_ROTATION:
        raise ValueError("lay_along must be one of %s" % sorted(LONG_AXIS_ROTATION))

    rotation = LONG_AXIS_ROTATION[lay_along]
    if any(rotation):
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        obj.rotation_euler = rotation

        # Baked into the mesh, because object rotation does not reliably survive export.
        bpy.ops.object.transform_apply(rotation=True)

    before, after = decimate(obj, target_triangles)

    bpy.context.view_layer.objects.active = obj

    # Flat shading: on decimated geometry the collapsed triangles become facets, which is
    # what stone and cut timber actually look like. Smooth shading turns them into potatoes.
    # (It also sidesteps use_auto_smooth, which Blender removed in 4.1.)
    if flat:
        bpy.ops.object.shade_flat()
    else:
        bpy.ops.object.shade_smooth()

    size = normalise_to_unit_box(obj)

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    path = os.path.join(OUT_DIR, name + ".obj")
    os.makedirs(OUT_DIR, exist_ok=True)

    # Blender is Z-up, Unity is Y-up. Exporting forward -Z / up Y hands Unity a mesh already
    # the right way round, rather than one lying on its side that every caller must rotate.
    bpy.ops.wm.obj_export(
        filepath=path,
        export_selected_objects=True,
        export_materials=False,
        export_uv=True,
        export_normals=True,
        export_triangulated_mesh=True,
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
    )

    print("[%s] %d triangles decimated to %d" % (name, before, after))
    print("[%s] pre-normalisation extent %.2f x %.2f x %.2f" % (name, size.x, size.y, size.z))

    # Read it back. Unity's Test Props checks this too, but catching it here means the
    # failure lands next to the line that caused it rather than three minutes later in a
    # different program — and an exporter flag that silently rescales is exactly the kind
    # of thing that would otherwise be found by a boulder looking wrong in the wood.
    verify_export(path, name)
    print("[%s] wrote %s" % (name, path))


def verify_export(path, name):
    """Parses the OBJ we just wrote and asserts the unit-box contract on the real file."""
    lo = [float("inf")] * 3
    hi = [float("-inf")] * 3
    faces = 0

    with open(path, "r") as handle:
        for line in handle:
            if line.startswith("v "):
                parts = [float(x) for x in line.split()[1:4]]
                for axis in range(3):
                    lo[axis] = min(lo[axis], parts[axis])
                    hi[axis] = max(hi[axis], parts[axis])
            elif line.startswith("f "):
                faces += 1

    size = [hi[axis] - lo[axis] for axis in range(3)]
    centre = [(hi[axis] + lo[axis]) * 0.5 for axis in range(3)]

    for axis, label in enumerate("XYZ"):
        if abs(size[axis] - 1.0) > 0.02:
            raise AssertionError(
                "%s: exported %s extent is %.3f, not 1.0 — the unit-box contract is broken"
                % (name, label, size[axis]))

        if abs(centre[axis]) > 0.02:
            raise AssertionError(
                "%s: exported %s centre is %.3f, not 0 — the prop is off-origin"
                % (name, label, centre[axis]))

    print("[%s] verified on disk: %d faces, unit box at the origin" % (name, faces))
