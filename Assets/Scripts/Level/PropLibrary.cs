using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Swaps a generated prop's box for a real mesh, when one exists.
    ///
    /// The levels are built out of `CreateBox` calls — a rock is a rotated cube, a ruined
    /// column is a long cube, the temple gate is four of them. That reads as programmer art
    /// because it is programmer art, and it is the most obvious remaining gap in the look
    /// now that the post stack is in.
    ///
    /// This is the seam that lets those be upgraded one at a time. A generator asks for a
    /// prop by name; if a mesh has been built for it the renderer uses that, and if not the
    /// box stays exactly as it was. Nothing breaks while the set is incomplete, which
    /// matters because there are hundreds of props and they will not all be done at once.
    ///
    /// **Two rules make this safe, and both are enforced by Test Props.**
    ///
    /// *The mesh replaces the visual only — never the collider.* The box collider the
    /// generator made stays exactly where it was, so the NavMesh bakes from identical
    /// geometry before and after. A prettier rock cannot seal a corridor, cannot strand a
    /// spawn, and cannot invalidate a single one of the six level verifications. Physics
    /// and pathing are decoupled from how things look, deliberately.
    ///
    /// *Every prop mesh is normalised to a unit bounding box centred on the origin.* The
    /// generators already size their boxes through localScale on a 1×1×1 cube, so a mesh
    /// that also measures 1×1×1 drops in with no scale maths anywhere. Get this wrong and a
    /// boulder arrives two metres across in a level that asked for one.
    /// </summary>
    public static class PropLibrary
    {
        /// <summary>Where the built meshes live. Resources, because the generators run at Awake.</summary>
        public const string ResourceFolder = "Props/";

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Names asked for that had no mesh, so a test can report the gap.</summary>
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>
        /// The mesh for a prop, or null if none has been built yet. Null is the normal
        /// case for most of the set and is not an error.
        /// </summary>
        public static Mesh Find(string prop)
        {
            if (string.IsNullOrEmpty(prop)) return null;

            Mesh cached;
            if (Cache.TryGetValue(prop, out cached)) return cached;

            Mesh mesh = Resources.Load<Mesh>(ResourceFolder + prop);
            Cache[prop] = mesh;

            if (mesh == null) Missing.Add(prop);
            return mesh;
        }

        /// <summary>
        /// Puts a prop mesh on an already-built box. Swaps the rendered mesh and nothing
        /// else — not the transform, not the scale, and above all not the collider.
        ///
        /// This used to take a `randomYaw` flag that spun the box about its up axis for
        /// variety, and that was a quiet violation of the one rule this class exists to
        /// keep: the transform carries the collider, so yawing it re-bakes the NavMesh.
        /// It showed up as the forest dropping from 5,969 navigation triangles to 5,967
        /// and the valley from 5,732 to 5,723 — small enough to shrug at, which is exactly
        /// what makes it worth catching. A rule that is obeyed except when the breach looks
        /// harmless is not a rule.
        ///
        /// Nothing was lost by removing it. Every generator already gives its props a
        /// random rotation of their own before dressing them, so the variety was there
        /// twice over and only one of the two was safe.
        /// </summary>
        public static bool Dress(GameObject box, string prop)
        {
            if (box == null) return false;

            Mesh mesh = Find(prop);
            if (mesh == null) return false;

            var filter = box.GetComponent<MeshFilter>();
            if (filter == null) return false;

            filter.sharedMesh = mesh;
            return true;
        }

        /// <summary>
        /// Hangs a prop mesh over geometry it is bigger than, without touching any collider.
        ///
        /// <see cref="Dress"/> covers the easy case — a rock mesh replacing a rock box, same
        /// volume, same collider. It does not cover a *desk*, because the box carrying the
        /// collider is only the tabletop while the mesh is the whole desk, legs and all.
        /// The tempting fix is to scale the box up to fit the mesh, and that is exactly
        /// wrong: it turns a tabletop into a solid block from floor to lid, changes what the
        /// NavMesh bakes, and can wall off a classroom that used to be walkable — the silent
        /// level-design change this system exists to prevent.
        ///
        /// So instead the mesh goes on a new collider-free child, sized and placed to cover
        /// the whole prop, and the boxes it replaces merely stop rendering. Collision is
        /// byte-for-byte what it was; only the picture changes.
        /// </summary>
        public static bool Overlay(GameObject anchor, string prop, Vector3 localCentre,
                                   Vector3 size, Material material, params GameObject[] hide)
        {
            if (anchor == null) return false;

            Mesh mesh = Find(prop);
            if (mesh == null) return false;

            var visual = new GameObject(prop + "_Visual");
            visual.transform.SetParent(anchor.transform, false);
            visual.transform.localPosition = localCentre;
            visual.transform.localScale = size;
            visual.isStatic = true;

            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;

            // The boxes stay — they are the collision — they just stop being drawn.
            foreach (GameObject box in hide)
            {
                if (box == null) continue;

                var renderer = box.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
            }

            return true;
        }

        /// <summary>Drops the cache so a rebuild picks up newly exported meshes.</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            Missing.Clear();
        }

        /// <summary>What has been asked for and does not exist yet.</summary>
        public static IEnumerable<string> MissingProps => Missing;

        /// <summary>How many distinct props resolved to a real mesh.</summary>
        public static int ResolvedCount
        {
            get
            {
                int count = 0;
                foreach (var pair in Cache)
                    if (pair.Value != null) count++;

                return count;
            }
        }

        /// <summary>
        /// True if a mesh honours the unit-bounding-box contract. The tolerance is loose
        /// on purpose: an exporter rounding to six decimals is fine, an exporter that
        /// forgot to normalise is off by whole metres.
        /// </summary>
        public static bool IsUnitSized(Mesh mesh, float tolerance = 0.02f)
        {
            if (mesh == null) return false;

            Bounds bounds = mesh.bounds;

            if (Mathf.Abs(bounds.size.x - 1f) > tolerance) return false;
            if (Mathf.Abs(bounds.size.y - 1f) > tolerance) return false;
            if (Mathf.Abs(bounds.size.z - 1f) > tolerance) return false;

            return bounds.center.magnitude <= tolerance;
        }
    }
}
