# -*- coding: utf-8 -*-
"""
The walkers' heads, sculpted from volumes rather than pushed about from a sphere.

    blender --background --python Tools_Props/zombiehead.py
    blender --background --python Tools_Props/zombiehead.py -- --render
    blender --background --python Tools_Props/zombiehead.py -- --render --closeup --only 0

WHY BLENDER, WHEN BodyMesh.Skull ALREADY EXISTS. The C# skull is a sphere whose radius is
nudged per direction. That can make a brow and a hollow temple, and it cannot make a hole:
every point on it is a single distance from the centre, so an orbit can be shallower than the
brow above it but never a pocket, and a nose can be flattened but never rotted away. The things
that make a dead face frightening are almost all holes. Blender builds the head as a solid,
carves the holes out of it with booleans, and voxel-remeshes the result into one clean closed
surface -- which C# could not reasonably do.

ONE FRAME FOR EVERYTHING. The first version of this built only the skull and left the jaw, the
teeth and the hair to C#, and the preview showed why that cannot work: a sculpted face over a
jaw that was a box, teeth that were a comb standing on the box, and hair that was a bowl. Four
systems, none of which knew where the others were. So every mesh here -- skin, cavities, jaw,
teeth, hair -- is written in the Skull part's own space, with the jaw already hanging open and
the lower teeth already rotated with it. The factory gives each of those parts the Skull part's
transform and nothing else, and they cannot disagree about where the mouth is.

WHAT COMES OUT, in Assets/Resources/Heads:

    ZombieHead<N>.obj        skin -- replaces the Skull part's mesh, inside its collider
    ZombieHead<N>Gore.obj    the carved wounds of head and jaw, dark and raw
    ZombieHead<N>Hair.obj    patchy scalp hair, grown off that variant's own skull
    ZombieJaw.obj            the mandible, hanging open
    ZombieTeethUpper.obj     arched, irregular, one missing
    ZombieTeethLower.obj     the same, rotated open with the jaw

Skin and gore are one surface cut along the rim of every wound, sharing the vertices on the
seam, so they meet exactly and nothing is drawn twice.

THE CONTRACT. The skin replaces the mesh of a part carrying the 2.5x critical sphere collider,
so it is clamped inside radius 0.5 of that part's space -- the rule BodyMesh follows. The jaw,
teeth, hair and gore carry no collider and are not clamped; they are cosmetic, like the box jaw
and the hair sphere they replace.

THE EYES STAY IN C#, and this script carves the orbits to fit them. That is two places that
must agree, which CLAUDE.md warns will disagree -- so Test Heads reads the eye parts' real
transforms off the built prefab and raycasts the exported meshes to prove the eye sits inside
its socket and the teeth are the first thing a ray from the front hits.

Deterministic throughout: every variant is seeded and every irregularity is a function of an
index, so a rebuild writes identical geometry.
"""

import math
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

from _propkit import clear_scene, noise3, PROJECT

OUT_DIR = os.path.join(PROJECT, "Assets", "Resources", "Heads")
PREVIEW_DIR = os.path.join(PROJECT, "Tools_Props", "_preview")
TEXTURES = os.path.join(PROJECT, "Assets", "Resources", "ProtoTextures")
MATERIALS = os.path.join(PROJECT, "Assets", "Resources", "ProtoMaterials")

# ---------------------------------------------------------------------------- the rig
# Copied from ZombieFactory.BuildTorso. Test Heads measures the meshes against the real part.
SKULL_CENTRE = (0.0, 0.08, 0.0)
SKULL_SCALE = (0.19, 0.23, 0.21)

# Just inside the primitive's 0.5, so decimation cannot push a vertex back out.
ENVELOPE = 0.492

# The eyes, copied from ZombieFactory.BuildFace. The orbits are carved around these.
EYE_X = 0.035
EYE_Y = 0.088
EYE_BALL_Z = 0.051
EYE_BALL_SEMI = (0.0096, 0.0088, 0.0080)

# The catchlight sits up and out on the eyeball, where a real one sits, and is tiny. The first
# closeup put a large bright dot dead centre, and a bright dot dead centre is a cartoon pupil.
#
# Its depth is computed from the eyeball rather than typed in, because the typed value put its
# front 0.1 mm BEHIND the eyeball's surface: for several renders the catchlight was invisible,
# and the highlight being admired was the eyeball's own specular.
EYE_GLINT_DX = 0.0028
EYE_GLINT_DY = 0.0028
EYE_GLINT_Z = EYE_BALL_Z + EYE_BALL_SEMI[2] * math.sqrt(
    1.0 - (EYE_GLINT_DX / EYE_BALL_SEMI[0]) ** 2 - (EYE_GLINT_DY / EYE_BALL_SEMI[1]) ** 2)
EYE_GLINT_SEMI = (0.0016, 0.0016, 0.0010)

# The jaw hangs from a hinge just in front of the ear, and drops this far open.
HINGE_Y = 0.030
HINGE_Z = -0.005
JAW_OPEN_DEGREES = 8.0

VOXEL = 0.0024
# Raised from 1400 after the closeups: at that count the brow and cheekbones collapsed into
# flat facets, and smooth shading across a big facet reads as a potato rather than as bone.
SKIN_TRIANGLES = 2200
JAW_TRIANGLES = 760
HAIR_TRIANGLES = 700

# How far inside the unwounded solid a face must be to count as wound. Larger than the skin
# noise and smoothing drift combined, so skin never turns to gore by accident.
WOUND_DEPTH = 0.0040

# ---------------------------------------------------------------------------- variants
VARIANTS = [
    # Both orbits deep, the nose gone, the left cheek torn through to the teeth.
    dict(seed=1301, tear=-1, orbit=((0.000, 1.00), (0.003, 1.06)), brow=1.00, dent=None,
         bald=0.42),
    # Older and more sunken; the right orbit slumped; a stove-in temple. No tear.
    dict(seed=2417, tear=0, orbit=((0.004, 1.10), (-0.002, 0.96)), brow=1.16,
         dent=(1, 0.105, 0.030), bald=0.62),
    # Bitten: the right cheek gone, a heavy brow, both orbits wide.
    dict(seed=3559, tear=1, orbit=((-0.001, 1.08), (0.001, 1.12)), brow=1.10, dent=None,
         bald=0.30),
    # Gaunt and narrow-eyed, with a split in the scalp.
    dict(seed=4673, tear=0, orbit=((0.002, 0.92), (0.002, 0.94)), brow=0.94,
         dent=(-1, 0.150, -0.020), bald=0.52),
]


# ---------------------------------------------------------------------------- frames

def B(ux, uy, uz):
    """Unity head-local metres to Blender coordinates. The face looks along Unity +Z."""
    return Vector((ux, -uz, uy))


def U(v):
    """Blender coordinates back to Unity head-local (x, y, z)."""
    return (v.x, v.z, -v.y)


def to_part(v):
    """Blender head-local to the Skull part's own space, in Unity axes."""
    ux, uy, uz = U(v)
    return ((ux - SKULL_CENTRE[0]) / SKULL_SCALE[0],
            (uy - SKULL_CENTRE[1]) / SKULL_SCALE[1],
            (uz - SKULL_CENTRE[2]) / SKULL_SCALE[2])


def hinge(ux, uy, uz, degrees=JAW_OPEN_DEGREES):
    """
    Rotates a Unity head-local point open about the jaw hinge.

    Unity's positive rotation about X carries +Z toward -Y, so the front of the jaw drops --
    the same sense as the Euler(11, 0, 0) the old box jaw was given.
    """
    c = math.cos(math.radians(degrees))
    s = math.sin(math.radians(degrees))
    dy = uy - HINGE_Y
    dz = uz - HINGE_Z
    return ux, HINGE_Y + c * dy - s * dz, HINGE_Z + s * dy + c * dz


# ---------------------------------------------------------------------------- building blocks

def add_ellipsoid(bm, centre, semi, segments=40, rings=22):
    """One closed ellipsoid shell, in Unity head-local metres."""
    geom = bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)
    c = B(*centre)
    for vert in geom["verts"]:
        ux, uy, uz = U(vert.co)
        vert.co = B(ux * semi[0], uy * semi[1], uz * semi[2]) + c


def mesh_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def apply(obj, modifier):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def remesh(obj):
    """Voxel remesh: any pile of overlapping shells becomes one closed surface. Union is free."""
    mod = obj.modifiers.new("Remesh", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = VOXEL
    mod.adaptivity = 0.0
    apply(obj, mod)


def smooth(obj, factor=0.55, iterations=6):
    mod = obj.modifiers.new("Smooth", "SMOOTH")
    mod.factor = factor
    mod.iterations = iterations
    apply(obj, mod)


def carve(obj, cutters):
    """
    Boolean difference, one cutter at a time. Cutters overlap each other -- a torn cheek runs
    into the mouth -- and a joined object of intersecting shells is not the closed solid an
    exact boolean insists on.
    """
    for index, (centre, semi) in enumerate(cutters):
        bm = bmesh.new()
        add_ellipsoid(bm, centre, semi, segments=28, rings=16)
        cutter = mesh_object("Cutter_%d" % index, bm)
        mod = obj.modifiers.new("Carve", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.object = cutter
        try:
            mod.solver = "EXACT"
        except TypeError:
            pass
        apply(obj, mod)
        bpy.data.objects.remove(cutter, do_unlink=True)


def solid(name, shells):
    bm = bmesh.new()
    for centre, semi in shells:
        add_ellipsoid(bm, centre, semi)
    return mesh_object(name, bm)


def triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()


def decimate(obj, target):
    triangulate(obj)
    before = len(obj.data.polygons)
    if before > target:
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = float(target) / float(before)
        apply(obj, mod)
        triangulate(obj)
    return before, len(obj.data.polygons)


def displace(obj, seed, lumps=0.0024, crinkle=0.0011):
    """
    Skin dried onto bone: slow lumps so the surface is not a mannequin, and a fine crinkle
    stretched horizontally so it reads as wrinkling rather than noise. Held under the wound
    depth so the cavity split below cannot mistake it for carving.
    """
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for vert in bm.verts:
        p = vert.co * 100.0
        a = noise3(p, 0.35, seed, octaves=3) * lumps
        b = noise3(Vector((p.x * 0.6, p.y * 0.6, p.z * 2.4)), 1.1, seed + 77, octaves=2) * crinkle
        vert.co += vert.normal * (a + b)
    bm.to_mesh(obj.data)
    bm.free()


def apply_fields(obj, fields):
    """
    Soft anatomy: smooth bumps and hollows pushed along the surface normal.

    THE REASON THIS EXISTS. The second and third closeups built the brow, the cheekbones and
    the gaunt hollows the same way as the holes -- as unions and boolean cuts -- and every one
    came out with a hard rim. The brow was a visor, the cheekbone a ledge running to the ear
    like the edge of a mask. A boolean rim is right for a real hole, where bone ends sharply; it
    is wrong for a contour, which is flesh and falls away gradually. So contours are a gaussian
    falloff applied to the dense remeshed surface, and booleans are kept for the holes.

    Each field is (centre, radii, amount) in Unity head-local metres; a positive amount pushes
    out, a negative one sinks in. Mirrored pairs are listed twice.
    """
    if not fields:
        return
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.normal_update()
    for vert in bm.verts:
        ux, uy, uz = U(vert.co)
        push = 0.0
        for (cx, cy, cz), (rx, ry, rz), amount in fields:
            d2 = ((ux - cx) / rx) ** 2 + ((uy - cy) / ry) ** 2 + ((uz - cz) / rz) ** 2
            if d2 < 9.0:
                push += amount * math.exp(-d2)
        if push:
            vert.co += vert.normal * push
    bm.to_mesh(obj.data)
    bm.free()


def clamp_to_envelope(obj):
    moved = 0
    for vert in obj.data.vertices:
        px, py, pz = to_part(vert.co)
        r = math.sqrt(px * px + py * py + pz * pz)
        if r <= ENVELOPE:
            continue
        k = ENVELOPE / r
        ux, uy, uz = U(vert.co)
        vert.co = B((ux - SKULL_CENTRE[0]) * k + SKULL_CENTRE[0],
                    (uy - SKULL_CENTRE[1]) * k + SKULL_CENTRE[1],
                    (uz - SKULL_CENTRE[2]) * k + SKULL_CENTRE[2])
        moved += 1
    return moved


def transform_points(obj, fn):
    """Applies a Unity-head-local point function to every vertex."""
    for vert in obj.data.vertices:
        vert.co = B(*fn(*U(vert.co)))


# ---------------------------------------------------------------------------- the arches
# One function places both the teeth and the pockets they stand in, so the two cannot drift.

def arch_points(half_width, front_z, depth, count_per_side, widths):
    """
    Points along a dental arch, centre outward, spaced by each tooth's width along the curve.

    A flat row of teeth on a round head is the single most fake-looking thing a mouth can do,
    which is what the first preview had. The arch is a parabola: the incisors at the front, and
    every tooth after them further round the curve and further back.
    """
    def at(x):
        t = x / half_width
        return x, front_z - depth * t * t

    # Arc length by fine sampling, so tooth widths are measured along the curve itself.
    samples = [at(half_width * i / 400.0) for i in range(401)]
    lengths = [0.0]
    for i in range(1, len(samples)):
        dx = samples[i][0] - samples[i - 1][0]
        dz = samples[i][1] - samples[i - 1][1]
        lengths.append(lengths[-1] + math.hypot(dx, dz))

    def point_at_length(s):
        for i in range(1, len(lengths)):
            if lengths[i] >= s:
                f = (s - lengths[i - 1]) / max(1e-9, lengths[i] - lengths[i - 1])
                x = samples[i - 1][0] + f * (samples[i][0] - samples[i - 1][0])
                z = samples[i - 1][1] + f * (samples[i][1] - samples[i - 1][1])
                # Tangent, for facing each tooth outward off the arch.
                tx = samples[i][0] - samples[i - 1][0]
                tz = samples[i][1] - samples[i - 1][1]
                return x, z, math.atan2(-tz, tx)
        x, z = samples[-1]
        return x, z, 0.0

    placed = []
    for side in (-1, 1):
        s = 0.0
        for k in range(count_per_side):
            w = widths[k]
            x, z, yaw = point_at_length(s + w * 0.5)
            placed.append((side, k, x * side, z, yaw * side, w))
            s += w
    return placed


UPPER = dict(half_width=0.046, front_z=0.079, depth=0.030, gum_y=-0.001,
             kinds=["incisor", "lateral", "canine", "premolar", "premolar"],
             widths=[0.0084, 0.0066, 0.0071, 0.0062, 0.0060],
             heights=[0.0112, 0.0094, 0.0118, 0.0086, 0.0080])

LOWER = dict(half_width=0.040, front_z=0.073, depth=0.027, gum_y=-0.031,
             kinds=["incisor", "lateral", "canine", "premolar", "premolar"],
             widths=[0.0056, 0.0058, 0.0066, 0.0064, 0.0062],
             heights=[0.0086, 0.0088, 0.0102, 0.0080, 0.0076])

# Irregularity by index: (side, k) -> what happened to that tooth. A missing tooth is worth
# more than the nine beside it; so is one snapped off short.
UPPER_FATE = {(-1, 1): "missing", (1, 0): "broken", (1, 3): "long"}
LOWER_FATE = {(1, 1): "missing", (-1, 0): "broken"}


def tooth(bm, kind, width, height, depth, fate, seed):
    """
    One tooth in its own frame: root at y = 0, tip at y = -height, facing +z.

    Shaped by kind rather than all identical: incisors flat and wide at the edge, canines drawn
    to a point, premolars blunt and squat. It is the silhouette of a row of different teeth that
    reads as a human mouth, where identical spikes read as a monster's.
    """
    if fate == "broken":
        height *= 0.55
    if fate == "long":
        height *= 1.18

    geom = bmesh.ops.create_cube(bm, size=1.0)
    verts = geom["verts"]

    taper = {"incisor": 0.86, "lateral": 0.80, "canine": 0.30, "premolar": 0.72}[kind]
    for vert in verts:
        tip = vert.co.y < 0.0
        x = vert.co.x * width
        z = vert.co.z * depth
        y = -height * (0.5 - vert.co.y)
        if tip:
            x *= taper
            z *= 0.78 if kind != "canine" else 0.45
        vert.co = Vector((x, y, z))

    if fate == "broken":
        # A snapped edge, not a neat short tooth: one corner of the tip lifted.
        for vert in verts:
            if vert.co.y < -height * 0.9 and vert.co.x > 0.0:
                vert.co.y += height * 0.35

    bmesh.ops.bevel(bm, geom=[e for e in bm.edges if all(v in verts for v in e.verts)],
                    offset=min(width, depth) * 0.16, segments=1, affect="EDGES", profile=0.6)
    return verts


def build_teeth(name, arch, fates, lower):
    bm = bmesh.new()
    placed = arch_points(arch["half_width"], arch["front_z"], arch["depth"],
                         len(arch["kinds"]), arch["widths"])

    for side, k, x, z, yaw, w in placed:
        fate = fates.get((side, k))
        if fate == "missing":
            continue

        before = set(bm.verts)
        tooth(bm, arch["kinds"][k], w * 0.92, arch["heights"][k], 0.0056, fate, k)
        new = [v for v in bm.verts if v not in before]

        # A little disorder: no tooth in a dead mouth is quite where it was.
        lean = math.sin((k + 1) * 2.399 * side) * 0.07
        twist = math.sin((k + 1) * 5.077 * side) * 0.12

        for vert in new:
            tx, ty, tz = vert.co
            # lean about z, then twist about y, in the tooth's own frame
            tx, ty = tx * math.cos(lean) - ty * math.sin(lean), tx * math.sin(lean) + ty * math.cos(lean)
            a = yaw + twist
            tx, tz = tx * math.cos(a) + tz * math.sin(a), -tx * math.sin(a) + tz * math.cos(a)
            if lower:
                ty = -ty
            ux, uy, uz = x + tx, arch["gum_y"] + ty, z + tz
            if lower:
                ux, uy, uz = hinge(ux, uy, uz)
            vert.co = B(ux, uy, uz)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    obj = mesh_object(name, bm)
    triangulate(obj)
    return obj


# ---------------------------------------------------------------------------- the head

# Below this height (Unity head-local) the head is allowed past the collider, because that is
# where the jaw and cheeks hang. Above it everything is pulled inside, so no part of the skull
# a player aims at is geometry they cannot hit.
CLAMP_ABOVE_Y = 0.030


def clamp_upper(obj):
    """Pull the upper head inside the collider. The lower face is left alone -- see partition."""
    moved = 0
    for vert in obj.data.vertices:
        ux, uy, uz = U(vert.co)
        if uy < CLAMP_ABOVE_Y:
            continue
        px, py, pz = to_part(vert.co)
        r = math.sqrt(px * px + py * py + pz * pz)
        if r <= ENVELOPE:
            continue
        k = ENVELOPE / r
        vert.co = B((ux - SKULL_CENTRE[0]) * k + SKULL_CENTRE[0],
                    (uy - SKULL_CENTRE[1]) * k + SKULL_CENTRE[1],
                    (uz - SKULL_CENTRE[2]) * k + SKULL_CENTRE[2])
        moved += 1
    return moved


def opened(shells):
    """Moves closed-mouth shells to where they sit with the jaw hanging open."""
    return [(hinge(*centre), semi) for centre, semi in shells]


def variant_shells(v):
    """
    Every volume of the head, jaw included, in one list -- because they are one solid.

    THE REASON. Three closeups built the skull and the jaw as separate sculpts, and every one
    had a seam down the side of the face: the skull must stay inside its collider, which on the
    sides ends near the mouth, and the jaw hung below it as a separate lump. The cheek flesh
    added to hide the join stood out beside the face like a thumb, twice. So now there is one
    sculpt with the jaw already open, and it is cut into a skull part and a jaw part afterwards
    along the collider -- both halves of one surface, sharing their vertices, in the same
    material. There is nothing left for a seam to be.
    """
    shells = [
        ((0.0, 0.086, -0.006), (0.086, 0.104, 0.094)),       # cranium
        ((0.0, 0.020, 0.048), (0.050, 0.036, 0.046)),        # maxilla
        ((0.0, -0.012, 0.004), (0.044, 0.030, 0.036)),       # back of the mouth, so it is a pocket
    ]
    for side, k, x, z, yaw, w in arch_points(UPPER["half_width"], UPPER["front_z"],
                                             UPPER["depth"], 5, UPPER["widths"]):
        shells.append(((x, 0.006, z - 0.004), (0.009, 0.012, 0.010)))     # upper gum ridge

    jaw = []
    for i in range(-9, 10):                                                # mandible body
        t = i / 9.0
        jaw.append(((0.052 * t, -0.046 + 0.006 * t * t, 0.066 - 0.070 * t * t),
                    (0.013, 0.016, 0.013)))
    jaw.append(((0.0, -0.057, 0.066), (0.015, 0.010, 0.010)))              # chin
    jaw.append(((0.0, -0.041, 0.028), (0.036, 0.011, 0.038)))              # floor of the mouth
    for side in (-1, 1):
        for i in range(6):                                                 # ramus
            f = i / 5.0
            jaw.append(((side * (0.050 + 0.004 * f), -0.046 + 0.070 * f, -0.004 * f),
                        (0.008, 0.013, 0.014)))
        for i in range(5):                                                 # cheek, up to the cheekbone
            f = i / 4.0
            jaw.append(((side * (0.047 + 0.003 * f), -0.034 + 0.058 * f, 0.030 + 0.020 * f),
                        (0.010, 0.017, 0.019)))
    for side, k, x, z, yaw, w in arch_points(LOWER["half_width"], LOWER["front_z"],
                                             LOWER["depth"], 5, LOWER["widths"]):
        jaw.append(((x, -0.036, z - 0.004), (0.008, 0.010, 0.009)))         # lower gum ridge

    return shells + opened(jaw)


def head_fields(v):
    """
    The contours of a starved face -- none of them a hole, so all of them stay skin.

    Amplitudes were raised after the fourth closeup, which had traded the hard rims for a head
    so smooth it read as an egg with holes in it.
    """
    brow = v["brow"]
    f = []
    for s in (-1, 1):
        f.append(((0.034 * s, 0.107, 0.088), (0.026, 0.011, 0.024), 0.0095 * brow))   # brow over the orbit
        f.append(((0.058 * s, 0.063, 0.064), (0.020, 0.010, 0.020), 0.0080))          # cheekbone
        f.append(((0.080 * s, 0.060, 0.022), (0.010, 0.007, 0.028), 0.0040))          # arch toward the ear
        f.append(((0.090 * s, 0.110, 0.020), (0.018, 0.030, 0.034), -0.0100))         # temple
        f.append(((0.058 * s, 0.020, 0.058), (0.022, 0.025, 0.030), -0.0110))         # under the cheekbone
        f.append(((0.068 * s, -0.012, 0.030), (0.022, 0.030, 0.040), -0.0050))        # jaw line hollow
    f.append(((0.0, 0.103, 0.094), (0.014, 0.011, 0.018), 0.0045))                     # glabella
    f.append(((0.0, 0.068, 0.100), (0.012, 0.020, 0.022), -0.0075))                    # nose bridge, pressed back
    f.append(((0.0, 0.070, -0.090), (0.050, 0.040, 0.030), 0.0050))                    # occipital
    return f


def head_wounds(v):
    """What turns to gore. Almost everything frightening about the face is here."""
    cuts = []

    # THE ORBITS. Small and deep: a real orbit is about three and a half centimetres, and the
    # dread is how far back the eye sits, not how large the socket is.
    for side, (drop, size) in zip((-1, 1), v["orbit"]):
        cuts.append(((EYE_X * side, EYE_Y + 0.002 - drop, 0.090),
                     (0.0205 * size, 0.0180 * size, 0.042)))

    # The nose, rotted to a hole. Two lobes: the septum is the last of it to go.
    cuts.append(((-0.0065, 0.048, 0.099), (0.0095, 0.018, 0.028)))
    cuts.append(((0.0065, 0.048, 0.099), (0.0095, 0.018, 0.028)))

    # The lips have receded off both gums: a pocket in front of every tooth, on the same arch
    # the tooth is placed on, so none of them is buried.
    for side, k, x, z, yaw, w in arch_points(UPPER["half_width"], UPPER["front_z"],
                                             UPPER["depth"], 5, UPPER["widths"]):
        cuts.append(((x * 1.04, -0.008, z + 0.010), (0.010, 0.016, 0.014)))
    lower = []
    for side, k, x, z, yaw, w in arch_points(LOWER["half_width"], LOWER["front_z"],
                                             LOWER["depth"], 5, LOWER["widths"]):
        lower.append(((x * 1.04, -0.026, z + 0.010), (0.009, 0.013, 0.013)))
    cuts += opened(lower)

    # The mouth itself. Carved rather than left as the gap between two shells, so its walls
    # are wound -- dark -- instead of pale skin seen through the teeth.
    # Narrow enough that it stays behind the cheeks. Wider, it broke out through the cheek
    # wall on a variant that was never meant to be torn.
    # Deep enough into the floor that its bottom is wound too. At 3 mm in, the floor sat under
    # WOUND_DEPTH, stayed skin, and showed as a pale disc ringed in dark inside every mouth.
    cuts.append(((0.0, -0.026, 0.044), (0.025, 0.015, 0.032)))

    if v["tear"]:
        s = v["tear"]
        cuts.append(((0.046 * s, 0.004, 0.064), (0.018, 0.017, 0.030)))
        cuts.append(((0.036 * s, -0.010, 0.070), (0.015, 0.014, 0.026)))

    if v["dent"]:
        s, y, z = v["dent"]
        cuts.append(((0.080 * s, y, z), (0.028, 0.024, 0.028)))

    return cuts


# Zones where anything visible is wound however shallow the carving. Inside a mouth or a nasal
# hole, depth is the wrong test: the back wall of the mouth was barely cut, sat under
# WOUND_DEPTH, stayed skin, and showed as a pale ring round the throat in every open mouth.
DARK_ZONES = [
    ((0.0, -0.026, 0.030), (0.034, 0.030, 0.050)),     # the mouth, down past the lower gum
    ((0.0, 0.048, 0.092), (0.018, 0.022, 0.020)),      # the nasal hole
]


def in_dark_zone(v):
    ux, uy, uz = U(v)
    for (cx, cy, cz), (rx, ry, rz) in DARK_ZONES:
        if ((ux - cx) / rx) ** 2 + ((uy - cy) / ry) ** 2 + ((uz - cz) / rz) ** 2 < 1.0:
            return True
    return False


def sculpt_variant(index, v):
    """
    Build, carve, remesh, roughen, decimate -- then cut into skull, jaw and wounds.

    The reference solid gets the same shells and fields but no wounds, and the same smoothing
    and clamping, so the only difference between it and the head is the wounds. A face more
    than WOUND_DEPTH inside it is wound.
    """
    name = "ZombieHead%d" % index
    shells, fields, wounds = variant_shells(v), head_fields(v), head_wounds(v)

    reference = solid(name + "_Ref", shells)
    remesh(reference)
    apply_fields(reference, fields)
    smooth(reference, 0.5, 4)
    clamp_upper(reference)

    obj = solid(name, shells)
    remesh(obj)
    apply_fields(obj, fields)
    carve(obj, wounds)
    remesh(obj)
    smooth(obj, 0.5, 4)
    displace(obj, v["seed"])

    dense = obj.copy()
    dense.data = obj.data.copy()
    dense.name = name + "_Dense"
    bpy.context.collection.objects.link(dense)

    before, after = decimate(obj, SKIN_TRIANGLES + JAW_TRIANGLES)
    moved = clamp_upper(obj)

    ref_bm = bmesh.new()
    ref_bm.from_mesh(reference.data)
    bvh = BVHTree.FromBMesh(ref_bm)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    wound, jaw = set(), set()
    for face in bm.faces:
        centre = face.calc_center_median()
        location, normal, _, distance = bvh.find_nearest(centre)
        deep = location is not None and (centre - location).dot(normal) < 0.0 and distance > WOUND_DEPTH
        if deep or in_dark_zone(centre):
            wound.add(face.index)
            continue
        # Skull or jaw: a face belongs to the skull only if every corner is inside the
        # collider, which makes the skull part inside it by construction rather than by a clamp.
        for vert in face.verts:
            px, py, pz = to_part(vert.co)
            if px * px + py * py + pz * pz > 0.25:
                jaw.add(face.index)
                break
    total = len(bm.faces)
    bm.free()
    ref_bm.free()
    bpy.data.objects.remove(reference, do_unlink=True)

    print("[%s] %d -> %d triangles, %d pulled inside; %d wound, %d jaw, %d skull"
          % (name, before, after, moved, len(wound), len(jaw), total - len(wound) - len(jaw)))

    skull = keep_faces(obj, lambda i: i not in wound and i not in jaw, name)
    jaw_part = keep_faces(obj, lambda i: i in jaw, name + "Jaw")
    gore = keep_faces(obj, lambda i: i in wound, name + "Gore")
    bpy.data.objects.remove(obj, do_unlink=True)

    hair = grow_hair(dense, v["seed"], v["bald"], name + "Hair")
    bpy.data.objects.remove(dense, do_unlink=True)
    return skull, jaw_part, gore, hair


def keep_faces(source, keep, name):
    bm = bmesh.new()
    bm.from_mesh(source.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not keep(f.index)], context="FACES")
    return mesh_object(name, bm)


def grow_hair(dense, seed, bald, name):
    """
    Hair grown off the skull's own dense surface: patchy, ragged, lifted in clumps.

    Two things learned the hard way. Grown off the decimated skin it came out as shards, its
    patches only as fine as the skin's triangles; grown off the dense voxel surface it came out
    as a staircase, because patches cut from a voxel-aligned grid follow the grid. So it is cut
    from the dense surface and then its boundary loop is relaxed and projected back onto the
    skull a few times, which rounds the steps into a hairline.
    """
    source = bmesh.new()
    source.from_mesh(dense.data)
    surface = BVHTree.FromBMesh(source)

    bm = bmesh.new()
    bm.from_mesh(dense.data)

    doomed = []
    for face in bm.faces:
        c = face.calc_center_median()
        px, py, pz = to_part(c)
        r = max(1e-6, math.sqrt(px * px + py * py + pz * pz))
        dy, dz = py / r, pz / r
        scalp = dy > 0.34 or (dz < 0.05 and dy > -0.10) or dz < -0.35
        if dz > 0.55 and dy < 0.62:
            scalp = False
        q = c * 100.0
        streak = noise3(Vector((q.x * 2.6, q.y * 2.6, q.z * 0.55)), 0.22, seed + 401, octaves=3)
        patch = noise3(q, 0.12, seed + 433, octaves=2)
        if not scalp or streak < -0.34 + bald * 0.5 or patch < -0.80 + bald * 0.5:
            doomed.append(face)

    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")

    for _ in range(6):
        moves = {}
        for vert in bm.verts:
            if not vert.is_boundary:
                continue
            ring = [e.other_vert(vert) for e in vert.link_edges if e.is_boundary]
            if len(ring) == 2:
                moves[vert] = vert.co * 0.4 + (ring[0].co + ring[1].co) * 0.3
        for vert, co in moves.items():
            location, _, _, _ = surface.find_nearest(co)
            vert.co = location if location is not None else co

    bm.normal_update()
    for vert in bm.verts:
        clump = noise3(vert.co * 100.0, 0.9, seed + 509, octaves=2)
        vert.co += vert.normal * (0.0034 + 0.0018 * clump)

    source.free()
    obj = mesh_object(name, bm)
    decimate(obj, HAIR_TRIANGLES)
    return obj


# ---------------------------------------------------------------------------- export

def write_uvs(obj):
    """
    Spherical UVs in the part's space, seam at the back, 0-1 -- the same convention as
    BodyMesh.Skull, so the material's own tiling means what it meant on the procedural head.
    """
    mesh = obj.data
    layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        us, vs = [], []
        for li in poly.loop_indices:
            px, py, pz = to_part(mesh.vertices[mesh.loops[li].vertex_index].co)
            r = max(1e-6, math.sqrt(px * px + py * py + pz * pz))
            us.append(math.atan2(px, pz) / (2.0 * math.pi) + 0.5)
            vs.append(math.asin(max(-1.0, min(1.0, py / r))) / math.pi + 0.5)
        if max(us) - min(us) > 0.5:
            us = [u + 1.0 if u < 0.5 else u for u in us]
        for li, u, v in zip(poly.loop_indices, us, vs):
            layer.data[li].uv = (u, v)


def export(obj, name, clamp_check):
    if not obj.data.uv_layers:
        write_uvs(obj)

    for vert in obj.data.vertices:
        px, py, pz = to_part(vert.co)
        vert.co = Vector((px, -pz, py))

    for poly in obj.data.polygons:
        poly.use_smooth = True

    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".obj")
    bpy.ops.wm.obj_export(filepath=path, export_selected_objects=True, export_materials=False,
                          export_uv=True, export_normals=True, export_triangulated_mesh=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y")
    verify(path, name, clamp_check)


def verify(path, name, clamp_check):
    worst, faces, uvs = 0.0, 0, 0
    with open(path, "r") as handle:
        for line in handle:
            if line.startswith("v "):
                x, y, z = (float(t) for t in line.split()[1:4])
                worst = max(worst, math.sqrt(x * x + y * y + z * z))
            elif line.startswith("vt "):
                uvs += 1
            elif line.startswith("f "):
                faces += 1
    if clamp_check and worst > 0.5 + 1e-3:
        raise AssertionError("%s reaches radius %.4f, outside the collider it is drawn over"
                             % (name, worst))
    if faces and not uvs:
        raise AssertionError("%s has no UVs" % name)
    print("[%s] on disk: %d triangles, widest %.4f%s" % (name, faces, worst,
          " (clamped)" if clamp_check else ""))


# ---------------------------------------------------------------------------- preview

def unity_material(key):
    """Colour and smoothness read off the game's own .mat, so the preview cannot drift from it."""
    path = os.path.join(MATERIALS, key + ".mat")
    colour, smoothness = (0.5, 0.5, 0.5), 0.2
    try:
        text = open(path, "r").read()
        m = re.search(r"- _Color: \{r: ([\d.eE-]+), g: ([\d.eE-]+), b: ([\d.eE-]+)", text)
        if m:
            colour = tuple(float(g) for g in m.groups())
        g = re.search(r"- _Glossiness: ([\d.eE-]+)", text)
        if g:
            smoothness = float(g.group(1))
    except OSError:
        pass
    return colour, smoothness


def srgb_to_linear(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def preview_material(name, key, surface=None, tiling=1.0, emission=None):
    colour, smoothness = unity_material(key)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = max(0.05, 1.0 - smoothness)
    tint = srgb_to_linear(colour)

    if surface:
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (tiling, tiling, 1.0)
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])

        albedo = nodes.new("ShaderNodeTexImage")
        albedo.image = bpy.data.images.load(os.path.join(TEXTURES, surface + "_albedo.png"))
        links.new(mapping.outputs["Vector"], albedo.inputs["Vector"])

        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = (*tint, 1.0)
        links.new(albedo.outputs["Color"], mix.inputs[6])
        links.new(mix.outputs[2], bsdf.inputs["Base Color"])

        normal = nodes.new("ShaderNodeTexImage")
        normal.image = bpy.data.images.load(os.path.join(TEXTURES, surface + "_normal.png"))
        normal.image.colorspace_settings.name = "Non-Color"
        links.new(mapping.outputs["Vector"], normal.inputs["Vector"])
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(normal.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    else:
        bsdf.inputs["Base Color"].default_value = (*tint, 1.0)

    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 0.9
    return mat


def fixed_material(name, colour, roughness, surface=None, tiling=1.0):
    """A preview material for something the game does not have a .mat for yet."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = roughness
    tint = srgb_to_linear(colour)
    if surface:
        coord = nodes.new("ShaderNodeTexCoord")
        mapping = nodes.new("ShaderNodeMapping")
        mapping.inputs["Scale"].default_value = (tiling, tiling, 1.0)
        links.new(coord.outputs["UV"], mapping.inputs["Vector"])
        albedo = nodes.new("ShaderNodeTexImage")
        albedo.image = bpy.data.images.load(os.path.join(TEXTURES, surface + "_albedo.png"))
        links.new(mapping.outputs["Vector"], albedo.inputs["Vector"])
        mix = nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs[7].default_value = (*tint, 1.0)
        links.new(albedo.outputs["Color"], mix.inputs[6])
        links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        normal = nodes.new("ShaderNodeTexImage")
        normal.image = bpy.data.images.load(os.path.join(TEXTURES, surface + "_normal.png"))
        normal.image.colorspace_settings.name = "Non-Color"
        links.new(mapping.outputs["Vector"], normal.inputs["Vector"])
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(normal.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    else:
        bsdf.inputs["Base Color"].default_value = (*tint, 1.0)
    return mat


def preview_assembly(meshes, offset, yaw_degrees, mats):
    """Everything the game puts on a head, parented under one pivot at the head bone."""
    pivot = bpy.data.objects.new("Pivot", None)
    pivot.location = offset
    pivot.rotation_euler = (0.0, 0.0, math.radians(yaw_degrees))
    bpy.context.collection.objects.link(pivot)

    for obj, mat in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        obj.parent = pivot

    def ellipsoid(name, centre, semi, mat):
        bm = bmesh.new()
        add_ellipsoid(bm, centre, semi, segments=24, rings=14)
        obj = mesh_object(name, bm)
        obj.data.materials.append(mat)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        obj.parent = pivot

    for side in (-1, 1):
        ellipsoid("Eye", (EYE_X * side, EYE_Y, EYE_BALL_Z), EYE_BALL_SEMI, mats["deadeye"])
        # Same side in both eyes: one light, one direction. Mirrored, it looked lit from two.
        ellipsoid("Glint", (EYE_X * side + EYE_GLINT_DX, EYE_Y + EYE_GLINT_DY, EYE_GLINT_Z),
                  EYE_GLINT_SEMI, mats["glint"])
    ellipsoid("Neck", (0.0, -0.07, -0.004), (0.043, 0.065, 0.043), mats["skin"])
    return pivot


def render(scene_objects, closeup, path):
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 64 if closeup else 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1100 if closeup else 2000
    scene.render.resolution_y = 1100 if closeup else 700
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0.0

    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.006, 0.006, 0.008, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    scene.world = world

    target = B(0.0, 0.05, 0.0)
    distance = 0.95 if closeup else 2.7

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 85
    cam = bpy.data.objects.new("Cam", cam_data)
    cam.location = target + B(0.0, 0.03, distance)
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.collection.objects.link(cam)
    scene.camera = cam

    # The torch: a spot from just beside the camera, which is how a zombie is actually met.
    torch_data = bpy.data.lights.new("Torch", "SPOT")
    torch_data.energy = 14.0 if closeup else 46.0
    torch_data.spot_size = math.radians(28 if closeup else 40)
    torch_data.spot_blend = 0.6
    torch_data.shadow_soft_size = 0.02
    torch = bpy.data.objects.new("Torch", torch_data)
    torch.location = cam.location + B(-0.14, -0.22, 0.0)
    torch.rotation_euler = (target - torch.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.collection.objects.link(torch)

    # And the room lamp overhead, which is what hoods the orbits.
    lamp_data = bpy.data.lights.new("Lamp", "AREA")
    lamp_data.energy = 26.0 if closeup else 68.0
    lamp_data.size = 0.6
    lamp = bpy.data.objects.new("Lamp", lamp_data)
    lamp.location = target + B(0.2, 0.9, 0.5)
    lamp.rotation_euler = (target - lamp.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.collection.objects.link(lamp)

    os.makedirs(PREVIEW_DIR, exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("[preview] wrote %s" % path)


# ---------------------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    rendering = "--render" in argv
    closeup = "--closeup" in argv
    only = None
    if "--only" in argv:
        only = int(argv[argv.index("--only") + 1])

    clear_scene()

    upper = build_teeth("ZombieTeethUpper", UPPER, UPPER_FATE, lower=False)
    lower = build_teeth("ZombieTeethLower", LOWER, LOWER_FATE, lower=True)

    heads = []
    for index, v in enumerate(VARIANTS):
        if only is not None and index != only:
            continue
        heads.append((index,) + sculpt_variant(index, v))

    if rendering:
        mats = dict(
            skin=preview_material("Skin", "skin", "flesh", 2.0),
            # A long-dead wound is black, not arterial red: dark is what the eye reads as depth.
            gore=fixed_material("Cavity", (0.085, 0.045, 0.038), 0.55, "flesh", 1.5),
            deadeye=fixed_material("DeadEye", (0.56, 0.55, 0.49), 0.18),
            hair=preview_material("Hair", "hair", "hair", 1.5),
            tooth=preview_material("Tooth", "tooth"),
            glint=preview_material("Glint", "eyeglint", emission=(0.9, 0.86, 0.72)),
        )
        spacing = 0.27
        count = len(heads)
        for slot, (index, skull, jaw, gore, hair) in enumerate(heads):
            offset = Vector(((slot - (count - 1) * 0.5) * spacing, 0.0, 0.0))
            up = upper.copy()
            up.data = upper.data.copy()
            bpy.context.collection.objects.link(up)
            lo = lower.copy()
            lo.data = lower.data.copy()
            bpy.context.collection.objects.link(lo)
            preview_assembly([(skull, mats["skin"]), (jaw, mats["skin"]), (gore, mats["gore"]),
                              (hair, mats["hair"]), (up, mats["tooth"]), (lo, mats["tooth"])],
                             offset, 0.0 if "--front" in argv else (-28.0 if closeup else -22.0), mats)
        upper.hide_render = True
        lower.hide_render = True
        name = ("front" if "--front" in argv else "closeup") if closeup else "heads"
        if only is not None:
            name += "_%d" % only
        render(None, closeup, os.path.join(PREVIEW_DIR, name + ".png"))
        return

    for index, skull, jaw, gore, hair in heads:
        for obj in (skull, jaw, gore, hair):
            write_uvs(obj)
        export(skull, "ZombieHead%d" % index, clamp_check=True)
        export(jaw, "ZombieHead%dJaw" % index, clamp_check=False)
        export(gore, "ZombieHead%dGore" % index, clamp_check=False)
        export(hair, "ZombieHead%dHair" % index, clamp_check=False)

    for obj, name in ((upper, "ZombieTeethUpper"), (lower, "ZombieTeethLower")):
        write_uvs(obj)
        export(obj, name, clamp_check=False)


if __name__ == "__main__":
    main()
