using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Generated textures, so particles and decals need no imported art.
    /// A soft radial falloff is all a smoke or dust particle actually needs; a splat is
    /// the same thing several times over at random offsets.
    /// </summary>
    public static class ProtoTextures
    {
        private static Texture2D _softCircle;
        private static Texture2D _splat;
        private static Material _particleMaterial;
        private static Material _decalMaterial;

        /// <summary>White with a smooth radial alpha falloff — the universal particle sprite.</summary>
        public static Texture2D SoftCircle
        {
            get
            {
                if (_softCircle != null) return _softCircle;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "SoftCircle",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                float centre = (size - 1) * 0.5f;
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - centre) / centre;
                        float dy = (y - centre) / centre;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                _softCircle = texture;
                return _softCircle;
            }
        }

        private static Texture2D _scopeMask;
        private static Sprite _scopeSprite;

        /// <summary>
        /// The black surround of a telescopic sight: opaque everywhere except a clear
        /// circle in the middle, with a dark ring around the glass and a soft inner
        /// shadow so the edge does not look like a hole cut with scissors.
        /// </summary>
        public static Sprite ScopeMask
        {
            get
            {
                if (_scopeSprite != null) return _scopeSprite;

                const int size = 512;
                const float clearRadius = 0.46f;   // fraction of the texture half-width
                const float ringWidth = 0.035f;

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "ScopeMask",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                float centre = (size - 1) * 0.5f;
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - centre) / centre;
                        float dy = (y - centre) / centre;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);

                        float alpha;
                        if (distance < clearRadius - ringWidth)
                        {
                            // Inside the glass: clear, with a slight vignette towards the edge.
                            float t = Mathf.InverseLerp(clearRadius - ringWidth, clearRadius * 0.55f, distance);
                            alpha = Mathf.Lerp(0.35f, 0f, t);
                        }
                        else if (distance < clearRadius)
                        {
                            // The ring itself, fading up to fully opaque.
                            alpha = Mathf.SmoothStep(0.35f, 1f, Mathf.InverseLerp(clearRadius - ringWidth, clearRadius, distance));
                        }
                        else
                        {
                            alpha = 1f;
                        }

                        pixels[y * size + x] = new Color32(0, 0, 0, (byte)(Mathf.Clamp01(alpha) * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                _scopeMask = texture;

                _scopeSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                _scopeSprite.name = "ScopeMask";
                _scopeSprite.hideFlags = HideFlags.HideAndDontSave;
                return _scopeSprite;
            }
        }

        /// <summary>An irregular blob built from overlapping circles — reads as a splat.</summary>
        public static Texture2D Splat
        {
            get
            {
                if (_splat != null) return _splat;

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "Splat",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };

                var alpha = new float[size * size];
                var rng = new System.Random(9182);

                // One big central blob plus satellites of decreasing size.
                AddBlob(alpha, size, 0.5f, 0.5f, 0.30f, rng);
                for (int i = 0; i < 9; i++)
                {
                    float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float distance = 0.16f + (float)rng.NextDouble() * 0.22f;
                    float radius = 0.05f + (float)rng.NextDouble() * 0.12f;
                    AddBlob(alpha,
                            size,
                            0.5f + Mathf.Cos(angle) * distance,
                            0.5f + Mathf.Sin(angle) * distance,
                            radius,
                            rng);
                }

                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte a = (byte)(Mathf.Clamp01(alpha[i]) * 255f);
                    pixels[i] = new Color32(255, 255, 255, a);
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                _splat = texture;
                return _splat;
            }
        }

        private static void AddBlob(float[] alpha, int size, float cx, float cy, float radius, System.Random rng)
        {
            // Slight per-blob wobble stops every splat reading as a circle.
            float wobble = 0.25f + (float)rng.NextDouble() * 0.5f;
            float phase = (float)(rng.NextDouble() * Mathf.PI * 2.0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (float)x / size - cx;
                    float dy = (float)y / size - cy;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    float localRadius = radius * (1f + Mathf.Sin(angle * 3f + phase) * 0.18f * wobble);
                    float value = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / Mathf.Max(0.001f, localRadius)));

                    int index = y * size + x;
                    alpha[index] = Mathf.Max(alpha[index], value);
                }
            }
        }

        /// <summary>Additive-friendly alpha-blended material used by every particle system.</summary>
        public static Material ParticleMaterial
        {
            get
            {
                if (_particleMaterial != null) return _particleMaterial;

                _particleMaterial = new Material(FindSpriteShader())
                {
                    name = "ProtoParticle",
                    mainTexture = SoftCircle,
                    hideFlags = HideFlags.HideAndDontSave
                };
                return _particleMaterial;
            }
        }

        public static Material DecalMaterial
        {
            get
            {
                if (_decalMaterial != null) return _decalMaterial;

                _decalMaterial = new Material(FindSpriteShader())
                {
                    name = "ProtoDecal",
                    mainTexture = Splat,
                    hideFlags = HideFlags.HideAndDontSave
                };
                return _decalMaterial;
            }
        }

        private static Shader FindSpriteShader()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }
    }
}
