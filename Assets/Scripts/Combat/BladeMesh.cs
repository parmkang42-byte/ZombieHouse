using System.Collections.Generic;
using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Generates a katana blade as a single continuous mesh.
    ///
    /// A cross-section is swept along a curved centreline: one unbroken surface with no
    /// seams, rather than boxes stacked end to end. The section is a wedge — sharp along
    /// the cutting edge, thickening towards the spine — which is what gives a blade its
    /// bright edge highlight as it turns in the light.
    ///
    /// The blade runs along +Y from the guard. X is thickness, Z is edge-to-spine.
    /// </summary>
    public static class BladeMesh
    {
        /// <param name="length">Blade length from guard to tip, metres.</param>
        /// <param name="curve">How far the tip falls back from straight (the sori).</param>
        /// <param name="segments">Rings along the blade. More is smoother.</param>
        public static Mesh CreateKatana(
            float length = 0.60f,
            float curve = 0.058f,
            float baseWidth = 0.034f,
            float tipWidth = 0.024f,
            float baseThickness = 0.0075f,
            float tipThickness = 0.0045f,
            int segments = 36)
        {
            segments = Mathf.Max(6, segments);

            var vertices = new List<Vector3>((segments + 1) * 4 + 1);
            var triangles = new List<int>(segments * 24 + 24);

            // --- sweep the cross-section along the curved centreline ------------
            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;

                Vector3 centre = CentreAt(u, length, curve);
                float angle = TangentAngle(u, length, curve);
                Quaternion ring = Quaternion.Euler(angle, 0f, 0f);

                // Taper towards the tip, then pinch hard over the last stretch so the
                // kissaki comes to a point instead of ending in a blunt stump.
                float taper = u < 0.9f ? 1f : Mathf.InverseLerp(1f, 0.9f, u);
                float halfWidth = Mathf.Lerp(baseWidth, tipWidth, u) * 0.5f * Mathf.Max(0.12f, taper);
                float halfThick = Mathf.Lerp(baseThickness, tipThickness, u) * 0.5f * Mathf.Max(0.12f, taper);

                // Four points per ring, walking around the section in a consistent order:
                // cutting edge, one flat, the spine, the other flat.
                vertices.Add(centre + ring * new Vector3(0f, 0f, -halfWidth));                 // edge
                vertices.Add(centre + ring * new Vector3(halfThick, 0f, halfWidth * 0.25f));   // flat +X
                vertices.Add(centre + ring * new Vector3(0f, 0f, halfWidth));                  // spine
                vertices.Add(centre + ring * new Vector3(-halfThick, 0f, halfWidth * 0.25f));  // flat -X
            }

            // --- skin between consecutive rings ---------------------------------
            for (int i = 0; i < segments; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;

                for (int k = 0; k < 4; k++)
                {
                    int k2 = (k + 1) % 4;
                    AddQuad(triangles, a + k, a + k2, b + k2, b + k);
                }
            }

            // --- tip -------------------------------------------------------------
            int tipIndex = vertices.Count;
            vertices.Add(CentreAt(1f, length, curve) + Quaternion.Euler(TangentAngle(1f, length, curve), 0f, 0f)
                         * new Vector3(0f, 0.012f, 0f));

            int last = segments * 4;
            for (int k = 0; k < 4; k++)
            {
                int k2 = (k + 1) % 4;
                triangles.Add(last + k);
                triangles.Add(last + k2);
                triangles.Add(tipIndex);
            }

            // --- base cap so the mesh is closed ---------------------------------
            triangles.Add(0); triangles.Add(2); triangles.Add(1);
            triangles.Add(0); triangles.Add(3); triangles.Add(2);

            // Winding decides which way the surface faces, and getting it backwards makes
            // the blade invisible from outside. Rather than trusting the hand-written
            // order, measure the enclosed volume: a closed mesh wound outwards encloses a
            // positive volume, so a negative result means every triangle is inside out.
            if (SignedVolume(vertices, triangles) < 0f) FlipWinding(triangles);

            var mesh = new Mesh { name = "KatanaBlade" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 CentreAt(float u, float length, float curve)
        {
            // Quadratic fall-back: straight at the guard, curving harder towards the tip.
            return new Vector3(0f, length * u, -curve * u * u);
        }

        private static float TangentAngle(float u, float length, float curve)
        {
            // d(centre)/du = (0, length, -2*curve*u); the ring must sit square to it.
            return Mathf.Atan2(-2f * curve * u, length) * Mathf.Rad2Deg;
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        /// <summary>Six times the signed volume of a closed mesh; sign follows the winding.</summary>
        public static float SignedVolume(List<Vector3> vertices, List<int> triangles)
        {
            float total = 0f;

            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                total += Vector3.Dot(v0, Vector3.Cross(v1, v2));
            }

            return total;
        }

        private static void FlipWinding(List<int> triangles)
        {
            for (int i = 0; i < triangles.Count; i += 3)
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        }
    }
}
