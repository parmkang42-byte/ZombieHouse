using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// A fixed ring of quads reused for bullet holes and blood. Nothing is ever
    /// instantiated at runtime, so a long firefight costs no allocations and the
    /// oldest marks quietly fade out as new ones are placed.
    /// </summary>
    public class DecalPool : MonoBehaviour
    {
        [SerializeField] private int capacity = 200;
        [SerializeField] private float lifetime = 300f;
        [SerializeField] private float fadeSeconds = 6f;
        [SerializeField] private float surfaceOffset = 0.012f;

        private Transform[] _transforms;
        private MeshRenderer[] _renderers;
        private float[] _placedAt;
        private Color[] _colours;
        private int _next;
        private MaterialPropertyBlock _mpb;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _transforms = new Transform[capacity];
            _renderers = new MeshRenderer[capacity];
            _placedAt = new float[capacity];
            _colours = new Color[capacity];

            for (int i = 0; i < capacity; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Decal_" + i;
                quad.transform.SetParent(transform, false);

                // Decals must never take part in physics or block bullets.
                var collider = quad.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                quad.layer = 2;   // Ignore Raycast

                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ProtoTextures.DecalMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                _transforms[i] = quad.transform;
                _renderers[i] = renderer;
                _placedAt[i] = float.NegativeInfinity;
            }
        }

        public void Place(Vector3 point, Vector3 normal, Color colour, float size)
        {
            if (_transforms == null || _transforms.Length == 0) return;
            if (normal.sqrMagnitude < 0.0001f) normal = Vector3.up;

            int index = _next;
            _next = (_next + 1) % _transforms.Length;

            Transform decal = _transforms[index];
            decal.position = point + normal * surfaceOffset;

            // Random roll around the surface normal so repeated hits do not stamp
            // an identical texture every time.
            decal.rotation = Quaternion.LookRotation(-normal, Vector3.up)
                             * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            decal.localScale = Vector3.one * size;

            _placedAt[index] = Time.time;
            _colours[index] = colour;
            _renderers[index].enabled = true;

            ApplyColour(index, colour.a);
        }

        private void Update()
        {
            float now = Time.time;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (!_renderers[i].enabled) continue;

                float age = now - _placedAt[i];
                if (age < lifetime - fadeSeconds) continue;

                if (age >= lifetime)
                {
                    _renderers[i].enabled = false;
                    continue;
                }

                float fade = 1f - (age - (lifetime - fadeSeconds)) / fadeSeconds;
                ApplyColour(i, _colours[i].a * fade);
            }
        }

        private void ApplyColour(int index, float alpha)
        {
            Color colour = _colours[index];
            colour.a = alpha;

            _renderers[index].GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, colour);
            _renderers[index].SetPropertyBlock(_mpb);
        }
    }
}
