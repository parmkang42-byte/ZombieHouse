using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Makes each walker an individual. A crowd of identical bodies reads as a video
    /// game; varying height, build, skin tone, clothing and posture is most of what
    /// sells a horde as a group of people who used to be different people.
    ///
    /// It also owns the hit flash, because it is the only thing that knows what colour
    /// each renderer is supposed to be after randomisation.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class ZombieAppearance : MonoBehaviour
    {
        [Header("Build")]
        [SerializeField] private Vector2 heightScale = new Vector2(0.92f, 1.07f);
        [SerializeField] private Vector2 widthScale = new Vector2(0.88f, 1.06f);

        [Header("Colour variation")]
        [SerializeField] private float skinValueSpread = 0.13f;
        [SerializeField] private float skinHueSpread = 0.03f;
        [SerializeField] private float clothingValueSpread = 0.3f;

        [Header("Posture (degrees)")]
        [SerializeField] private Vector2 headTilt = new Vector2(-16f, 16f);
        [SerializeField] private Vector2 headTurn = new Vector2(-12f, 12f);
        [SerializeField] private Vector2 shoulderDroop = new Vector2(0f, 14f);
        [SerializeField] private Vector2 limpSeverity = new Vector2(0f, 0.75f);

        [Header("Hit flash")]
        [SerializeField] private Color flashColour = new Color(1f, 0.92f, 0.9f);
        [SerializeField] private float flashSeconds = 0.1f;

        /// <summary>Read by ZombieVisuals so the walk is asymmetric per individual.</summary>
        public float LimpSeverity { get; private set; }
        public int LimpSide { get; private set; } = 1;
        public float HeadTiltDegrees { get; private set; }
        public float HeadTurnDegrees { get; private set; }
        public float ShoulderDroopDegrees { get; private set; }

        /// <summary>Per-zombie speed multiplier — some walkers are faster than others.</summary>
        public float PaceMultiplier { get; private set; } = 1f;

        private Renderer[] _renderers;
        private Color[] _baseColours;
        private MaterialPropertyBlock _mpb;
        private float _flashUntil = -1f;
        private bool _flashing;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            Randomise();
        }

        /// <summary>
        /// Rolls this zombie's build, tint and posture. Public because Awake does not run in
        /// edit mode, so anything inspecting a freshly created zombie there — a test
        /// measuring whether a boss fits under a ceiling, for instance — would otherwise be
        /// looking at an unscaled 1x body and drawing confident conclusions from it.
        /// </summary>
        public void Initialise()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Randomise();
        }

        private void Randomise()
        {
            var rig = GetComponent<ZombieRig>();

            // --- build -------------------------------------------------------
            // The archetype sets the baseline size; the random spread on top keeps two
            // walkers of the same type from being identical.
            var profile = GetComponent<ZombieProfile>();
            float archetypeScale = profile != null && profile.Archetype != null ? profile.Archetype.Scale : 1f;
            Color archetypeTint = profile != null && profile.Archetype != null
                ? profile.Archetype.SkinTint
                : Color.white;

            // Bosses are authored, not rolled. The 0.92-1.07 spread exists so that forty
            // walkers do not look like one walker stamped forty times, and that reasoning
            // simply does not apply to something there is exactly one of. Worse, it made the
            // boss a different size every run: the house's giant has a 10 cm margin under a
            // 4.2 m ceiling, so a high roll put its head through the floor above on some runs
            // and not others — the least debuggable kind of bug there is.
            //
            // Weight == 0 is the existing marker for "never drawn at random", which every
            // boss archetype already sets, so this needs no new flag.
            bool authored = profile != null && profile.Archetype != null && profile.Archetype.Weight == 0f;

            float height = authored
                ? archetypeScale
                : Random.Range(heightScale.x, heightScale.y) * archetypeScale;
            float width = Random.Range(widthScale.x, widthScale.y);

            // Height scales the whole rig UNIFORMLY on purpose. A non-uniform rig scale
            // squashes the capsule colliders, and the ragdoll's joints then behave
            // unpredictably. Build variation comes from the clothing instead, which has
            // no colliders and so cannot upset physics.
            if (rig != null && rig.Bones != null && rig.Bones.Rig != null)
                rig.Bones.Rig.localScale = Vector3.one * height;

            WidenClothing(width);

            // Shorter walkers shuffle a little quicker; it is a small thing that stops
            // a group from moving like one object.
            PaceMultiplier = Mathf.Lerp(1.08f, 0.94f, Mathf.InverseLerp(heightScale.x, heightScale.y, height));

            // --- posture -----------------------------------------------------
            HeadTiltDegrees = Random.Range(headTilt.x, headTilt.y);
            HeadTurnDegrees = Random.Range(headTurn.x, headTurn.y);
            ShoulderDroopDegrees = Random.Range(shoulderDroop.x, shoulderDroop.y);
            LimpSeverity = Random.Range(limpSeverity.x, limpSeverity.y);
            LimpSide = Random.value < 0.5f ? -1 : 1;

            // --- colour ------------------------------------------------------
            _renderers = GetComponentsInChildren<Renderer>();
            _baseColours = new Color[_renderers.Length];

            float skinShift = Random.Range(-skinValueSpread, skinValueSpread);
            float skinHue = Random.Range(-skinHueSpread, skinHueSpread);
            float clothShift = Random.Range(-clothingValueSpread, clothingValueSpread);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                Color colour = renderer.sharedMaterial != null ? renderer.sharedMaterial.color : Color.white;

                string materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;

                if (materialName.Contains("skin"))
                    colour = ShiftColour(colour, skinHue, skinShift) * archetypeTint;
                else if (materialName.Contains("shirt") || materialName.Contains("trousers"))
                    colour = ShiftColour(colour, 0f, clothShift);

                colour.a = 1f;

                _baseColours[i] = colour;
                ApplyColour(i, colour);
            }
        }

        /// <summary>
        /// Thickens or thins the clothing layer so walkers read as different builds.
        /// Only collider-free cosmetic parts are touched.
        /// </summary>
        private void WidenClothing(float width)
        {
            foreach (Transform part in GetComponentsInChildren<Transform>())
            {
                if (part.GetComponent<Collider>() != null) continue;

                string name = part.name;
                bool isClothing = name.StartsWith("Shirt") || name.StartsWith("Waistband")
                                  || name.StartsWith("TrouserLeg") || name.StartsWith("Sleeve");
                if (!isClothing) continue;

                Vector3 scale = part.localScale;
                part.localScale = new Vector3(scale.x * width, scale.y, scale.z * width);
            }
        }

        private static Color ShiftColour(Color colour, float hueShift, float valueShift)
        {
            float h, s, v;
            Color.RGBToHSV(colour, out h, out s, out v);

            h = Mathf.Repeat(h + hueShift, 1f);
            v = Mathf.Clamp01(v + valueShift);
            s = Mathf.Clamp01(s * Random.Range(0.85f, 1.15f));

            Color shifted = Color.HSVToRGB(h, s, v);
            shifted.a = colour.a;
            return shifted;
        }

        private void Update()
        {
            if (!_flashing) return;
            if (Time.time < _flashUntil) return;

            RestoreColours();
            _flashing = false;
        }

        /// <summary>Whites out the body for a moment so hits register visually.</summary>
        public void FlashHit()
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
                ApplyColour(i, flashColour);

            _flashUntil = Time.time + flashSeconds;
            _flashing = true;
        }

        private void RestoreColours()
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
                ApplyColour(i, _baseColours[i]);
        }

        private void ApplyColour(int index, Color colour)
        {
            Renderer renderer = _renderers[index];
            if (renderer == null) return;

            renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, colour);
            _mpb.SetColor(ColorId, colour);
            renderer.SetPropertyBlock(_mpb);
        }

        /// <summary>Darkens the corpse slightly so bodies on the floor read as dead, not stunned.</summary>
        public void ApplyDeadTint(float darken = 0.72f)
        {
            if (_renderers == null) return;

            _flashing = false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color colour = _baseColours[i] * darken;
                colour.a = _baseColours[i].a;
                _baseColours[i] = colour;
                ApplyColour(i, colour);
            }
        }
    }
}
