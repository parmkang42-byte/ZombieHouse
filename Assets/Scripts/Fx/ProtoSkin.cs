using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Generated surface detail for bodies and clothing, so a walker stops reading as
    /// painted plastic without the project acquiring an art dependency.
    ///
    /// WHY THIS EXISTS. Every material in the game was a flat colour with a smoothness
    /// value and nothing else. A flat albedo on a smooth capsule is the definition of
    /// plastic: there is no detail anywhere for the light to catch, so the eye gets one
    /// shading gradient per limb and reads the whole thing as a toy. Geometry does not
    /// fix that — a tapered limb made of the same flat colour is a better-shaped toy.
    ///
    /// WHAT COMES OUT. Two maps that agree with each other, which is the part that
    /// matters. The albedo and the normal are generated from the same height field, so
    /// a crease is dark *and* indented and a blister is pale *and* raised. Decoupled
    /// maps — noise on one, different noise on the other — look worse than no maps at
    /// all, because the shading contradicts the pigment and the brain notices.
    ///
    /// THE ALBEDO IS A MULTIPLIER, NOT A COLOUR. It averages to white and modulates
    /// around it, so <c>material.color</c> keeps meaning exactly what it meant before
    /// and every existing tone, archetype tint and per-zombie shift downstream still
    /// lands where it used to. One generator therefore serves skin, gore, hair, shirt
    /// and trousers without any of their palettes being re-tuned. Hue variation still
    /// works, because a multiplier is subtractive: times (1.00, 0.92, 0.86) is a warm
    /// jaundiced patch, times (0.88, 0.96, 1.06) is a cold lividity bruise.
    ///
    /// EVERYTHING TILES. These are applied to primitives with stock UVs and repeated
    /// two to four times across a limb, so a seam would be visible on every zombie in
    /// the game. The noise lattice wraps at its own period, which makes the result
    /// seamless by construction rather than by a mirrored-edge trick that shows up as
    /// a line of symmetry.
    ///
    /// It is deterministic: the same seed gives the same texture on every machine and
    /// every rebuild, so a build is reproducible and the generated .png assets do not
    /// churn in git for no reason.
    /// </summary>
    public static class ProtoSkin
    {
        /// <summary>An albedo and the normal map generated from the same height field.</summary>
        public struct Surface
        {
            public Texture2D Albedo;
            public Texture2D Normal;

            /// <summary>
            /// What the material's colour must be multiplied by to undo the encoding.
            ///
            /// A multiplier that averages white cannot be stored in a byte texture,
            /// because some of it is brighter than white. So the whole map is divided
            /// down by its own maximum on the way in, and this is that number: multiply
            /// the old flat colour by it and the textured material averages out to
            /// exactly the tone it had before. That round trip is what let every body
            /// material get detail without one entry of the palette being re-tuned, and
            /// Test Skin asserts it rather than trusting the arithmetic.
            /// </summary>
            public float Headroom;
        }

        /// <summary>
        /// Default resolution. 256 is enough for detail that is only ever seen across a
        /// corridor at 2-4 tiles per limb, and keeps the generated .png assets small
        /// enough that committing them is not a nuisance.
        /// </summary>
        public const int DefaultSize = 256;

        // ------------------------------------------------------------------ flesh

        /// <summary>
        /// Decayed skin: lividity pooling in large soft patches, capillary marbling,
        /// scattered necrotic pits, and pore grain over all of it.
        ///
        /// The scales are chosen to be legible at different distances, which is the
        /// whole trick with a texture seen mostly in the dark. Lividity is the only
        /// thing readable across a room; marbling appears at conversational distance;
        /// pores exist only to stop the surface being perfectly smooth under a
        /// flashlight held a metre away.
        /// </summary>
        public static Surface Flesh(int seed, int size = DefaultSize)
        {
            var height = new float[size * size];
            var albedo = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float u = x / (float)size;
                    float v = y / (float)size;

                    // --- the height field ------------------------------------
                    // Pores are the fine grain; creases are stretched along one axis so
                    // they read as skin pulled over bone rather than as generic lumps.
                    float pores = Fbm(u, v, 32, 32, 3, seed + 11);
                    float creases = Fbm(u, v, 10, 3, 4, seed + 23);

                    // Necrosis eats pits into the surface. Thresholded low frequency, so
                    // patches are few, large and irregular rather than evenly speckled.
                    float rot = Fbm(u, v, 5, 5, 3, seed + 37);
                    float pitting = Mathf.InverseLerp(0.46f, 0.30f, rot);

                    float h = 0.42f * pores + 0.38f * creases + 0.20f * (1f - pitting);
                    height[i] = h;

                    // --- the albedo ------------------------------------------
                    // Lividity: blood settles, so one side of a patch goes cold purple
                    // and the other warm yellow. One low-frequency field drives both
                    // ends of that, which keeps them from ever overlapping.
                    float livid = Fbm(u, v, 3, 3, 3, seed + 51);
                    Color pool = Color.Lerp(
                        new Color(0.86f, 0.93f, 1.06f),   // cold, pooled, bruised
                        new Color(1.06f, 0.98f, 0.84f),   // warm, jaundiced, dried out
                        Smooth(livid));

                    // Marbling: ridged noise gives thin dark lines instead of blobs,
                    // which is what broken-down capillaries under thin skin look like.
                    float veins = Ridged(u, v, 14, 14, 3, seed + 67);
                    float vein = Mathf.InverseLerp(0.72f, 0.97f, veins) * 0.28f;

                    // Necrotic patches are darker and much less saturated than skin.
                    float necrotic = pitting * 0.42f;

                    // Pore grain, kept small — this is texture, not pattern.
                    float grain = (pores - 0.5f) * 0.13f;

                    Color c = pool;
                    c *= 1f + grain;
                    c *= 1f - necrotic;
                    c.r *= 1f - vein * 0.55f;
                    c.g *= 1f - vein;
                    c.b *= 1f - vein * 0.75f;

                    albedo[i] = c;
                }
            }

            return Build(albedo, height, size, "Flesh", seed, normalStrength: 2.4f);
        }

        // ------------------------------------------------------------------ cloth

        /// <summary>
        /// Worn fabric: a woven grid at thread scale, ground-in dirt at patch scale,
        /// and thin places where the weave has worn through.
        ///
        /// The weave is generated rather than drawn as stripes because a regular grid
        /// aliases into moire the moment it is minified, and a zombie is usually far
        /// away. Perturbing the thread positions with noise breaks the regularity
        /// enough that it dissolves into cloth at distance instead of shimmering.
        /// </summary>
        public static Surface Cloth(int seed, int size = DefaultSize)
        {
            var height = new float[size * size];
            var albedo = new Color[size * size];

            const int Threads = 40;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float u = x / (float)size;
                    float v = y / (float)size;

                    // --- weave -----------------------------------------------
                    // Warp and weft, each a raised cosine, each nudged off true by a
                    // little noise so the grid never lines up perfectly with itself.
                    float wobbleU = (Fbm(u, v, 8, 8, 2, seed + 5) - 0.5f) * 0.35f;
                    float wobbleV = (Fbm(u, v, 8, 8, 2, seed + 9) - 0.5f) * 0.35f;

                    float warp = Mathf.Cos((u * Threads + wobbleU) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float weft = Mathf.Cos((v * Threads + wobbleV) * Mathf.PI * 2f) * 0.5f + 0.5f;

                    // Over-under: whichever thread is on top at this pixel is the one
                    // that catches the light, which is what makes fabric read as woven
                    // rather than as a checkerboard.
                    float weave = Mathf.Max(warp, weft) * 0.75f + Mathf.Min(warp, weft) * 0.25f;

                    // --- wear ------------------------------------------------
                    float dirt = Fbm(u, v, 4, 4, 3, seed + 17);
                    float grime = Mathf.InverseLerp(0.58f, 0.30f, dirt) * 0.34f;

                    float thin = Fbm(u, v, 7, 7, 4, seed + 29);
                    float threadbare = Mathf.InverseLerp(0.60f, 0.78f, thin);

                    height[i] = weave * (1f - threadbare * 0.7f);

                    // Dirt is warm and desaturating; worn places are paler because the
                    // dye has gone out of them, not darker.
                    Color c = Color.white;
                    c *= 1f - grime;
                    c.r *= 1f + grime * 0.30f;
                    c.g *= 1f + grime * 0.12f;

                    c *= 0.88f + weave * 0.24f;
                    c *= 1f + threadbare * 0.22f;

                    albedo[i] = c;
                }
            }

            return Build(albedo, height, size, "Cloth", seed, normalStrength: 1.5f);
        }

        // ------------------------------------------------------------------ hair

        /// <summary>
        /// Matted hair: strands running one way, clumped and greasy in places.
        /// Strongly anisotropic — the noise is stretched about twelve to one — because
        /// that direction is the only thing that separates hair from noise.
        /// </summary>
        public static Surface Hair(int seed, int size = DefaultSize)
        {
            var height = new float[size * size];
            var albedo = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float u = x / (float)size;
                    float v = y / (float)size;

                    // Many lattice cells across, few down: strands.
                    float strands = Fbm(u, v, 32, 3, 3, seed + 13);
                    float clumps = Fbm(u, v, 6, 3, 3, seed + 31);

                    float h = strands * 0.7f + clumps * 0.3f;
                    height[i] = h;

                    // Greasy clumps are darker and shinier; dry ends are paler.
                    float grease = Mathf.InverseLerp(0.40f, 0.62f, clumps);

                    Color c = Color.white;
                    c *= 0.72f + strands * 0.52f;
                    c *= 1f - grease * 0.22f;

                    albedo[i] = c;
                }
            }

            return Build(albedo, height, size, "Hair", seed, normalStrength: 2.0f);
        }

        // ------------------------------------------------------------------ shared

        /// <summary>
        /// Turns a height field into a normal map by central differences.
        ///
        /// The result is written in DXT5nm layout — x in alpha, y in green, red and
        /// blue filled — which is what Unity's UnpackNormal expects on desktop. When
        /// these are saved as .png assets the importer is told they are normal maps and
        /// does its own encoding; this path is for the runtime fallback, where no
        /// importer has run and the bytes have to already be right.
        /// </summary>
        private static Texture2D NormalFrom(float[] height, int size, float strength)
        {
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Wrap the neighbours so the normal map tiles as seamlessly as the
                    // height field it came from.
                    float left = height[y * size + Wrap(x - 1, size)];
                    float right = height[y * size + Wrap(x + 1, size)];
                    float down = height[Wrap(y - 1, size) * size + x];
                    float up = height[Wrap(y + 1, size) * size + x];

                    Vector3 n = new Vector3((left - right) * strength,
                                            (down - up) * strength,
                                            1f).normalized;

                    byte nx = (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255);
                    byte ny = (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255);

                    pixels[y * size + x] = new Color32(255, ny, 255, nx);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "ProtoNormal",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Normalises a float multiplier map into a byte texture, and reports the factor
        /// needed to undo it.
        ///
        /// Two steps, and the order matters. First each channel is scaled so its mean is
        /// exactly 1, which strips any accidental colour cast out of the generator and
        /// leaves the material's own colour as the only thing deciding the tone. Then the
        /// whole map is divided by its maximum, so the brightest pixel lands exactly on
        /// 255 and nothing clips — clamping here would quietly darken the result and put
        /// the round trip out by however much it clipped.
        /// </summary>
        private static Surface Build(Color[] albedo, float[] height, int size,
                                     string label, int seed, float normalStrength)
        {
            double sumR = 0d, sumG = 0d, sumB = 0d;
            for (int i = 0; i < albedo.Length; i++)
            {
                sumR += albedo[i].r;
                sumG += albedo[i].g;
                sumB += albedo[i].b;
            }

            float meanR = (float)(sumR / albedo.Length);
            float meanG = (float)(sumG / albedo.Length);
            float meanB = (float)(sumB / albedo.Length);

            // A generator that produced a flat black channel would divide by zero here;
            // none does, but the guard costs nothing and the alternative is a NaN texture.
            float scaleR = meanR > 1e-5f ? 1f / meanR : 1f;
            float scaleG = meanG > 1e-5f ? 1f / meanG : 1f;
            float scaleB = meanB > 1e-5f ? 1f / meanB : 1f;

            float peak = 0f;
            for (int i = 0; i < albedo.Length; i++)
            {
                albedo[i] = new Color(albedo[i].r * scaleR,
                                      albedo[i].g * scaleG,
                                      albedo[i].b * scaleB);
                peak = Mathf.Max(peak, Mathf.Max(albedo[i].r, Mathf.Max(albedo[i].g, albedo[i].b)));
            }

            if (peak < 1e-5f) peak = 1f;

            var pixels = new Color32[albedo.Length];
            for (int i = 0; i < albedo.Length; i++)
            {
                pixels[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(albedo[i].r / peak * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(albedo[i].g / peak * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(albedo[i].b / peak * 255f), 0, 255),
                    255);
            }

            var map = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Proto" + label + "_" + seed,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            map.SetPixels32(pixels);
            map.Apply();

            var normal = NormalFrom(height, size, normalStrength);
            normal.name = "Proto" + label + "Normal_" + seed;

            return new Surface { Albedo = map, Normal = normal, Headroom = peak };
        }

        // ------------------------------------------------------------------ noise

        private static int Wrap(int v, int period)
        {
            int m = v % period;
            return m < 0 ? m + period : m;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed * 374761393 + x * 668265263 + y * 915488749;
                h = (h ^ (h >> 13)) * 1274126177;
                h = (h ^ (h >> 16)) * unchecked((int)2246822519u);
                h ^= h >> 13;
                return (h & 0x7fffffff) / 2147483647f;
            }
        }

        /// <summary>
        /// Value noise on a lattice that wraps at its own period, which is what makes
        /// every texture here tile without a seam.
        /// </summary>
        private static float ValueNoise(float x, float y, int periodX, int periodY, int seed)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            int xa = Wrap(x0, periodX);
            int xb = Wrap(x0 + 1, periodX);
            int ya = Wrap(y0, periodY);
            int yb = Wrap(y0 + 1, periodY);

            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);

            float top = Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), sx);
            float bottom = Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), sx);
            return Mathf.Lerp(top, bottom, sy);
        }

        /// <summary>
        /// Summed octaves. Separate periods per axis are what allow a stretched feature
        /// — a crease, a hair strand — while still tiling in both directions.
        /// </summary>
        private static float Fbm(float u, float v, int periodX, int periodY, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            int px = periodX;
            int py = periodY;

            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * ValueNoise(u * px, v * py, px, py, seed + i * 101);
                total += amplitude;
                amplitude *= 0.5f;
                px *= 2;
                py *= 2;
            }

            return sum / total;
        }

        /// <summary>
        /// Ridged noise: folded around its midpoint so the peaks become thin lines
        /// rather than round blobs. Veins and cracks are lines; ordinary fBm cannot
        /// make one.
        /// </summary>
        private static float Ridged(float u, float v, int periodX, int periodY, int octaves, int seed)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            int px = periodX;
            int py = periodY;

            for (int i = 0; i < octaves; i++)
            {
                float n = ValueNoise(u * px, v * py, px, py, seed + i * 101);
                sum += amplitude * (1f - Mathf.Abs(n * 2f - 1f));
                total += amplitude;
                amplitude *= 0.5f;
                px *= 2;
                py *= 2;
            }

            return sum / total;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
