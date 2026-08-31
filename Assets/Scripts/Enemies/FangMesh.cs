using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// A single tooth: a four-sided spike, base at the origin and tip at +Y one unit up,
    /// so the thing that places it controls length and thickness entirely with scale.
    ///
    /// Built rather than imported for the same reason as the katana blade — the project
    /// has no art dependencies — and hard-edged on purpose: every face gets its own
    /// vertices so the light breaks along the ridges instead of rounding them off, which
    /// is what makes a tooth read as sharp at the distance you actually see a bear.
    /// </summary>
    public static class FangMesh
    {
        /// <param name="taper">
        /// How far the tip leans back over the base, 0–1. A little lean reads as a fang
        /// that curves inwards rather than a plain pyramid.
        /// </param>
        public static Mesh Create(float taper = 0.18f)
        {
            var mesh = new Mesh { name = "Fang" };

            float h = 0.5f;
            Vector3 apex = new Vector3(0f, 1f, -taper);

            // Base corners, wound clockwise seen from above.
            Vector3[] baseCorners =
            {
                new Vector3(-h, 0f, -h),
                new Vector3(h, 0f, -h),
                new Vector3(h, 0f, h),
                new Vector3(-h, 0f, h)
            };

            var vertices = new Vector3[16];
            var triangles = new int[18];

            // Four sides, each its own triangle with its own vertices.
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = baseCorners[i];
                Vector3 b = baseCorners[(i + 1) % 4];

                int v = i * 3;
                vertices[v] = a;
                vertices[v + 1] = b;
                vertices[v + 2] = apex;

                int t = i * 3;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
            }

            // The base, so the tooth is closed where it meets the gum.
            for (int i = 0; i < 4; i++) vertices[12 + i] = baseCorners[i];
            triangles[12] = 12; triangles[13] = 13; triangles[14] = 14;
            triangles[15] = 12; triangles[16] = 14; triangles[17] = 15;

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
