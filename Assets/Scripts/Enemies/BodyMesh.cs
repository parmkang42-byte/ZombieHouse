using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// The parts of a body whose shape a primitive cannot make.
    ///
    /// WHY. A capsule is the same width all the way down. A thigh is not: it is wide at
    /// the hip and narrow at the knee, and so is every other limb segment on a person.
    /// Silhouette is the first thing the eye reads and very nearly the only thing it gets
    /// at the range a zombie is usually seen — across a dark room, in fog, backlit. Six
    /// identical-width tubes and a ball read as a snowman however good the surface on
    /// them is.
    ///
    /// The skull is the other half. It is the part the player looks at and the part they
    /// shoot at, and a sphere has none of the things that make a skull recognisable: the
    /// brow ridge, the sunken temples, the bulge at the back, the way it narrows towards
    /// the jaw.
    ///
    /// WHAT THIS MUST NOT CHANGE. The colliders. Every one of these replaces a primitive's
    /// *mesh* and leaves the primitive's own collider alone, which is the same rule
    /// PropLibrary.Dress follows and for the same reason: the hitboxes are gameplay, the
    /// silhouette is not. So the head is still a sphere to shoot at and the thigh is still
    /// a capsule, and no shot that used to land stops landing.
    ///
    /// That constraint is why nothing here ever exceeds the radius of the primitive it
    /// replaces. Growing past it would put visible geometry outside the collider — a brow
    /// you can see and cannot hit — so every profile is built to reach 1.0 at most and
    /// sits under it nearly everywhere.
    /// </summary>
    public static class BodyMesh
    {
        /// <summary>The parts that get a generated mesh. Clothing reuses the limb under it.</summary>
        public enum Part { Thigh, Shin, UpperArm, Forearm, Skull, Teeth }

        /// <summary>
        /// Whether a part stands in for a primitive that carries a collider.
        ///
        /// The limbs and the skull do, so their geometry has to stay inside the capsule or
        /// sphere it is drawn over — anything outside is something the player can see and
        /// cannot shoot. Teeth carry no collider at all, exactly like the jaw box they sit
        /// in, so that constraint does not apply and applying it anyway would only force
        /// a mouth too small to read.
        /// </summary>
        public static bool WearsACollider(Part part) => part != Part.Teeth;

        private static readonly Dictionary<Part, Mesh> Cache = new Dictionary<Part, Mesh>();

        /// <summary>
        /// The shared mesh for one part, preferring a real asset.
        ///
        /// Same shape as ProtoMaterials.Get, and for the same reason one level down: a
        /// mesh built in memory does not survive being saved into a prefab, so the prefab
        /// ships referring to nothing and the limb renders as an invisible hole. The
        /// editor writes these out as .asset files; this is the runtime fallback so play
        /// mode works before a build has ever run.
        /// </summary>
        public static Mesh Shared(Part part)
        {
            Mesh cached;
            if (Cache.TryGetValue(part, out cached) && cached != null) return cached;

            var asset = Resources.Load<Mesh>("ProtoMeshes/" + part.ToString().ToLowerInvariant());
            if (asset != null)
            {
                Cache[part] = asset;
                return asset;
            }

            Mesh built = Build(part);
            built.hideFlags = HideFlags.HideAndDontSave;
            Cache[part] = built;
            return built;
        }

        /// <summary>Drops cached lookups so newly created .asset meshes are picked up.</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>
        /// Builds one part from scratch. Public so the editor can write it out as an asset
        /// without going through the cache and picking up whatever is already on disk.
        /// </summary>
        public static Mesh Build(Part part)
        {
            switch (part)
            {
                // Wide at the hip, narrow at the knee, with the mass of the quadriceps
                // sitting high on the bone.
                case Part.Thigh: return Limb("Thigh", 1f, 0.66f, 0.07f, 0.72f);

                // The calf is the strongest taper on a body: a thick belly just under the
                // knee falling away to almost nothing at the ankle.
                case Part.Shin: return Limb("Shin", 0.94f, 0.52f, 0.11f, 0.70f);

                case Part.UpperArm: return Limb("UpperArm", 1f, 0.70f, 0.06f, 0.68f);
                case Part.Forearm: return Limb("Forearm", 0.92f, 0.54f, 0.08f, 0.74f);

                case Part.Teeth: return Teeth();

                default: return Skull();
            }
        }

        // ------------------------------------------------------------------ limbs

        /// <summary>
        /// A tapered tube with rounded ends, laid along Y from -1 to +1 with a maximum
        /// radius of 0.5 — the same extents as Unity's capsule, so it drops straight in
        /// under the scales the factory already uses and nothing has to be re-measured.
        ///
        /// +Y is the joint end. Limbs hang downward from their pivot, so for a thigh that
        /// is the hip and for a shin it is the knee, which is why every taper here runs
        /// from wide at the top to narrow at the bottom.
        /// </summary>
        /// <param name="top">Radius at the joint end, 1 being the full capsule radius.</param>
        /// <param name="bottom">Radius at the far end.</param>
        /// <param name="bulge">Extra radius at the muscle belly.</param>
        /// <param name="bulgeAt">Where that belly sits, 0 at the bottom and 1 at the top.</param>
        private static Mesh Limb(string name, float top, float bottom, float bulge, float bulgeAt)
        {
            const int Segments = 12;
            const int Rings = 14;

            var vertices = new Vector3[(Rings + 1) * (Segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Rings * Segments * 6];

            for (int r = 0; r <= Rings; r++)
            {
                float v = r / (float)Rings;              // 0 at the far end, 1 at the joint
                float y = -1f + 2f * v;

                float taper = Mathf.Lerp(bottom, top, v);

                float d = (v - bulgeAt) / 0.28f;
                float belly = 1f + bulge * Mathf.Exp(-d * d);

                // Round the last tenth at each end into a cap. Held just off zero so the
                // final ring is a small disc rather than a degenerate point — a ring of
                // zero-area triangles gives RecalculateNormals nothing to work with and
                // leaves a black rim where the limb meets the joint.
                float edge = Mathf.Abs(v - 0.5f) * 2f;
                float cap = edge <= 0.82f
                    ? 1f
                    : Mathf.Max(0.14f, Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((edge - 0.82f) / 0.18f, 2f))));

                float radius = 0.5f * Mathf.Min(1f, taper * belly) * cap;

                for (int seg = 0; seg <= Segments; seg++)
                {
                    float angle = seg / (float)Segments * Mathf.PI * 2f;
                    int i = r * (Segments + 1) + seg;

                    vertices[i] = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

                    // Cylindrical, with the seam duplicated at seg == Segments so the
                    // generated skin does not mirror itself across the join.
                    uv[i] = new Vector2(seg / (float)Segments, v);
                }
            }

            int t = 0;
            for (int r = 0; r < Rings; r++)
            {
                for (int seg = 0; seg < Segments; seg++)
                {
                    int a = r * (Segments + 1) + seg;
                    int b = a + Segments + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;

                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            return Finish(name, vertices, uv, triangles);
        }

        // ------------------------------------------------------------------ skull

        /// <summary>
        /// A head, on a unit sphere's extents so it drops in where the sphere was.
        ///
        /// Everything here is carved out of a slightly undersized ball rather than added
        /// to a full-sized one. The base is 0.90 and the features add at most 0.10, so the
        /// result touches 1.0 at the brow and the occiput and sits inside the sphere
        /// collider everywhere else. A skull that grew past it would have places you can
        /// see and cannot shoot, and the head is the critical hitbox.
        /// </summary>
        private static Mesh Skull()
        {
            const int Segments = 20;
            const int Rings = 16;

            var vertices = new Vector3[(Rings + 1) * (Segments + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Rings * Segments * 6];

            for (int r = 0; r <= Rings; r++)
            {
                float polar = r / (float)Rings * Mathf.PI;
                float y = Mathf.Cos(polar);
                float ring = Mathf.Sin(polar);

                for (int seg = 0; seg <= Segments; seg++)
                {
                    float angle = seg / (float)Segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * ring;
                    float z = Mathf.Sin(angle) * ring;

                    int i = r * (Segments + 1) + seg;

                    float f = 0.90f;

                    // The back of the cranium, which on a real head is the widest and
                    // furthest-back point and on a sphere is nothing at all.
                    f += 0.10f * Bump(z, -0.75f, 0.85f) * Bump(y, 0.15f, 0.95f);

                    // The brow. A ridge across the front above where the eyes are, and the
                    // single feature that most makes a head read as a skull rather than a
                    // ball with a face drawn on it.
                    f += 0.11f * Bump(z, 0.80f, 0.45f) * Bump(y, 0.34f, 0.26f);

                    // Temples, scooped in at the sides above the cheekbone. This is the
                    // hollow that reads as gaunt.
                    f -= 0.08f * Bump(Mathf.Abs(x), 0.88f, 0.35f) * Bump(y, 0.30f, 0.42f);

                    // THE ORBITS, and they are the whole face.
                    //
                    // Eyes were two flat discs painted on the front of a sphere. What
                    // frightens anybody about a face is not what is in the sockets but how
                    // far back it is: a deep orbit under a heavy brow is dark whatever the
                    // light is doing, and the dark is what the eye reads as a skull.
                    //
                    // This only became worth doing once the level lights started casting.
                    // Before that a hollow was shaded the same as a bulge and carving one
                    // bought nothing at all.
                    float orbit = Bump(Mathf.Abs(x), 0.52f, 0.32f)
                                * Bump(y, 0.12f, 0.36f)
                                * Bump(z, 0.84f, 0.42f);
                    f -= 0.17f * orbit;

                    // The nose has gone. Not a wound, just absent — the cartilage is the
                    // first thing to go and what is left is a notch above the teeth.
                    f -= 0.11f * Bump(Mathf.Abs(x), 0f, 0.24f)
                               * Bump(y, -0.04f, 0.30f)
                               * Bump(z, 0.95f, 0.32f);

                    // Cheekbones, out and forward.
                    f += 0.05f * Bump(Mathf.Abs(x), 0.72f, 0.30f)
                               * Bump(y, -0.18f, 0.28f) * Bump(z, 0.55f, 0.60f);

                    // The head narrows towards the jaw rather than closing as a hemisphere.
                    f -= 0.13f * Bump(y, -0.95f, 0.75f);

                    // And it is flatter over the crown than a sphere is.
                    f -= 0.05f * Bump(y, 1f, 0.35f);

                    float radius = 0.5f * Mathf.Min(1f, f);

                    vertices[i] = new Vector3(x * radius, y * radius, z * radius);
                    uv[i] = new Vector2(seg / (float)Segments, 1f - r / (float)Rings);
                }
            }

            int t = 0;
            for (int r = 0; r < Rings; r++)
            {
                for (int seg = 0; seg < Segments; seg++)
                {
                    int a = r * (Segments + 1) + seg;
                    int b = a + Segments + 1;

                    triangles[t++] = a;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;

                    triangles[t++] = a + 1;
                    triangles[t++] = b + 1;
                    triangles[t++] = b;
                }
            }

            return Finish("Skull", vertices, uv, triangles);
        }

        // ------------------------------------------------------------------ teeth

        /// <summary>
        /// One row of teeth as a single mesh, gum at y = 0 and tips at y = -1.
        ///
        /// ONE MESH, NOT TWELVE OBJECTS. A tooth apiece would be twelve extra GameObjects
        /// on every body in the level — five hundred and fifty across a full house — for
        /// something that never moves relative to the jaw it sits in. The whole row is
        /// welded into one mesh and placed as one part, which costs two parts a head.
        ///
        /// The row is deliberately ragged. Even teeth read as a costume; what is
        /// unsettling is the irregularity — one shorter, one turned, one simply gone. The
        /// unevenness is a function of the tooth's index rather than a random number, so
        /// the mesh is identical on every rebuild and the .asset does not churn.
        ///
        /// The lower row is the same mesh turned through 180 degrees, which flips it
        /// point-upward and mirrors it across the mouth. A rotation preserves winding
        /// where a negative scale would invert it and turn the row inside out.
        /// </summary>
        private static Mesh Teeth()
        {
            const int Count = 9;

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < Count; i++)
            {
                float t = Count > 1 ? i / (float)(Count - 1) : 0.5f;
                float centre = Mathf.Lerp(-0.42f, 0.42f, t);

                // Deterministic raggedness. Two incommensurate waves so the pattern does
                // not repeat across the row and read as a texture.
                float wobble = Mathf.Sin(i * 2.399f) * 0.5f + 0.5f;
                float second = Mathf.Sin(i * 5.077f) * 0.5f + 0.5f;

                // One gap. A missing tooth is worth more than the nine that are there.
                if (i == 2) continue;

                float length = Mathf.Lerp(0.55f, 1f, wobble);
                float halfWidth = Mathf.Lerp(0.030f, 0.052f, second);
                float halfDepth = 0.030f;

                // A little lean, so nothing in the mouth is quite straight.
                float lean = (second - 0.5f) * 0.18f;

                Vector3 apex = new Vector3(centre + lean * length, -length, 0f);

                Vector3[] baseRing =
                {
                    new Vector3(centre - halfWidth, 0f, -halfDepth),
                    new Vector3(centre + halfWidth, 0f, -halfDepth),
                    new Vector3(centre + halfWidth, 0f, halfDepth),
                    new Vector3(centre - halfWidth, 0f, halfDepth),
                };

                // Every face gets its own vertices so the light breaks along the ridges
                // instead of rounding them off — the same reason FangMesh is built this
                // way, and the reason a tooth reads as sharp at the distance you meet one.
                for (int e = 0; e < 4; e++)
                {
                    Vector3 a = baseRing[e];
                    Vector3 b = baseRing[(e + 1) % 4];

                    int v = vertices.Count;
                    vertices.Add(a);
                    vertices.Add(b);
                    vertices.Add(apex);

                    uv.Add(new Vector2(0f, 0f));
                    uv.Add(new Vector2(1f, 0f));
                    uv.Add(new Vector2(0.5f, 1f));

                    triangles.Add(v);
                    triangles.Add(v + 2);
                    triangles.Add(v + 1);
                }

                // Close the gum end, so a tooth seen from below is not a hollow shell.
                int baseStart = vertices.Count;
                foreach (Vector3 corner in baseRing)
                {
                    vertices.Add(corner);
                    uv.Add(new Vector2(corner.x - centre + 0.5f, corner.z + 0.5f));
                }

                triangles.Add(baseStart); triangles.Add(baseStart + 1); triangles.Add(baseStart + 2);
                triangles.Add(baseStart); triangles.Add(baseStart + 2); triangles.Add(baseStart + 3);
            }

            return Finish("Teeth", vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }

        // ------------------------------------------------------------------ shared

        /// <summary>
        /// A smooth bump: 1 where <paramref name="value"/> sits on <paramref name="centre"/>
        /// and falling to 0 at <paramref name="width"/> either side. Multiplying two or
        /// three of these together is how a feature gets confined to one region of the
        /// head without any branching, and it stays smooth, which matters because a
        /// hard-edged deformation shows up as a crease under a moving light.
        /// </summary>
        private static float Bump(float value, float centre, float width)
        {
            float d = Mathf.Abs(value - centre) / width;
            if (d >= 1f) return 0f;
            float k = 1f - d * d;
            return k * k;
        }

        private static Mesh Finish(string name, Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();   // the normal maps from ProtoSkin need these
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
