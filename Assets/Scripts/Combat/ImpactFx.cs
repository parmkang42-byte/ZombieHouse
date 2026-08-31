using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// Placeholder shooting feedback built from primitives so Stage 1 needs no art assets:
    /// a fading tracer line and a small impact flash. Swap for particle systems in Stage 2.
    /// </summary>
    public static class ImpactFx
    {
        private static Material _unlit;

        private static Material UnlitMaterial
        {
            get
            {
                if (_unlit == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("Unlit/Color");
                    if (shader == null) shader = Shader.Find("Standard");
                    _unlit = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                return _unlit;
            }
        }

        public static void Tracer(Vector3 from, Vector3 to, Color color, float lifetime = 0.05f)
        {
            var go = new GameObject("Tracer") { hideFlags = HideFlags.HideAndDontSave };
            var line = go.AddComponent<LineRenderer>();
            line.material = UnlitMaterial;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.startWidth = 0.02f;
            line.endWidth = 0.005f;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Object.Destroy(go, lifetime);
        }

        public static void Impact(Vector3 point, Vector3 normal, Color color, float size = 0.09f, float lifetime = 0.12f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Impact";
            go.hideFlags = HideFlags.HideAndDontSave;
            Object.Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(UnlitMaterial) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = color;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.transform.position = point + normal * 0.02f;
            go.transform.localScale = Vector3.one * size;
            Object.Destroy(go, lifetime);
        }
    }
}
