using UnityEngine;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// The camera's post stack: bloom, tonemap, colour grade, vignette and grain.
    ///
    /// This project had none, and it was the largest single thing holding the look back —
    /// larger than the fact that every creature is made of spheres. Half the game's
    /// atmosphere is emissive: torches down a tomb corridor, a bear's eyes in the dark,
    /// pink horse eyes, the muzzle flash, the school's fluorescents. Without bloom an
    /// emissive surface is just a brightly coloured shape, so the emission colours in
    /// <c>ProtoMaterials</c> had all been pushed past 2.0 to read at all — compensating in
    /// the material for something missing in the pipeline. With a stack in place they can
    /// come back down and actually glow.
    ///
    /// Written by hand rather than using com.unity.postprocessing. That package would work,
    /// but it ships its own shaders and textures, and this project's defining property is
    /// that it has no imported assets at all — the audio is synthesised, the textures are
    /// generated, the geometry is primitives. A stack that is three shader passes and one
    /// component belongs here in a way a package does not.
    ///
    /// **Where it lives.** On the world camera, not the weapon camera. The rig has two: the
    /// world camera, and a weapon camera at depth+1 that clears depth only and draws the
    /// view model over the top. Putting the stack on the world camera means the gun in your
    /// hands is not graded or vignetted — which is a common and defensible choice, and more
    /// importantly it is the arrangement that behaves predictably. Chaining an image effect
    /// onto the second camera of a depth-only pair works, but it depends on the colour
    /// buffer surviving between the two, and that is exactly the sort of thing that renders
    /// correctly on one machine and black on another. Almost everything worth blooming —
    /// every torch, every pair of eyes, every fluorescent tube — is on the world camera
    /// anyway. Move the component to the weapon camera if you want the muzzle flash graded
    /// too, and look at it before believing it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public class PostProcessStack : MonoBehaviour
    {
        [Header("Bloom")]
        [Tooltip("How much of the blurred bright pass is added back. This is the knob that "
                 + "matters; everything else is trim.")]
        [SerializeField] private float bloomIntensity = 0.85f;

        [Tooltip("Brightness at which a pixel starts to bloom. Above 1 means only genuinely "
                 + "over-bright things glow, which needs HDR on the camera.")]
        [SerializeField] private float threshold = 1.05f;

        [Tooltip("Width of the soft knee below the threshold. Zero gives a hard edge that "
                 + "visibly crawls across a wall as a torch flickers.")]
        [SerializeField] private float knee = 0.45f;

        [Tooltip("Blur rounds. Each one widens the glow; four is a soft, wide halo and one "
                 + "is a tight rim. Cost is linear.")]
        [Range(1, 6)] [SerializeField] private int blurIterations = 4;

        [Header("Grade")]
        [SerializeField] private float exposure = 1f;
        [SerializeField] private float contrast = 1.06f;
        [SerializeField] private float saturation = 1f;

        [Tooltip("Multiplied into the image before tonemapping. This is where a level's "
                 + "colour identity lives — the tomb is warm, the valley is green-grey.")]
        [SerializeField] private Color colourFilter = Color.white;

        [Header("Vignette")]
        [SerializeField] private float vignetteIntensity = 1.1f;
        [SerializeField] private float vignetteSmoothness = 1.5f;

        [Header("Grain")]
        [Tooltip("Breaks up the banding that flat colours produce across a dark wall, and "
                 + "reads as film while it does it.")]
        [SerializeField] private float grainIntensity = 0.035f;
        [SerializeField] private bool animateGrain = true;

        private const string ShaderResourcePath = "Shaders/ZombiePost";
        private const string ShaderName = "ZombieHouse/Post";

        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int KneeId = Shader.PropertyToID("_Knee");
        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ColourFilterId = Shader.PropertyToID("_ColourFilter");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int VignetteSmoothnessId = Shader.PropertyToID("_VignetteSmoothness");
        private static readonly int GrainIntensityId = Shader.PropertyToID("_GrainIntensity");
        private static readonly int GrainSeedId = Shader.PropertyToID("_GrainSeed");

        private Material _material;
        private Camera _camera;

        /// <summary>The shader this runs on. Null means the stack is a passthrough.</summary>
        public Shader Effect => Resolve();

        public bool Ready => Effect != null;
        public float BloomIntensity => bloomIntensity;
        public float Threshold => threshold;
        public Color ColourFilter => colourFilter;

        /// <summary>
        /// Sets a level's look in one call, so the mood lives beside the rest of that
        /// level's setup rather than being buried in serialized fields on a camera.
        /// </summary>
        public void Configure(float bloom, float bloomThreshold, Color filter,
                              float grade, float saturate, float vignette, float grain)
        {
            bloomIntensity = bloom;
            threshold = bloomThreshold;
            colourFilter = filter;
            contrast = grade;
            saturation = saturate;
            vignetteIntensity = vignette;
            grainIntensity = grain;
        }

        /// <summary>
        /// Finds the shader, preferring Resources so this still works in a player build.
        /// Shader.Find alone resolves nothing that is not in Resources or in Always
        /// Included Shaders, so relying on it would give a stack that works in the editor
        /// and silently vanishes when built.
        /// </summary>
        private static Shader Resolve()
        {
            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null) shader = Shader.Find(ShaderName);
            return shader;
        }

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();

            // Bloom needs headroom above 1 or the threshold has nothing to find: the
            // emissive materials are the whole point of having it, and they are the only
            // things in the scene that go over-bright.
            if (_camera != null) _camera.allowHDR = true;
        }

        private void OnDisable()
        {
            if (_material == null) return;

            if (Application.isPlaying) Destroy(_material);
            else DestroyImmediate(_material);

            _material = null;
        }

        /// <summary>
        /// Builds the working material on demand. Kept out of Awake deliberately: Awake
        /// does not run in edit mode, and this component runs in edit mode, so a test or a
        /// scene view that never entered play would otherwise be looking at a null.
        /// </summary>
        private Material Prepare()
        {
            if (_material != null) return _material;

            Shader shader = Resolve();
            if (shader == null || !shader.isSupported) return null;

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _material;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Render(source, destination);
        }

        /// <summary>
        /// The whole effect, source to destination. Split out of OnRenderImage so a test
        /// can push a known frame through it and read the result back — the same reasoning
        /// as TickReload and TickCooldown, one layer up: anything only reachable from a
        /// Unity callback is only testable by playing the game.
        /// </summary>
        public void Render(RenderTexture source, RenderTexture destination)
        {
            Material material = Prepare();
            if (material == null || bloomIntensity <= 0f && grainIntensity <= 0f
                                 && Mathf.Approximately(exposure, 1f))
            {
                // Nothing to do, or nothing to do it with: hand the frame straight through
                // rather than tinting it by accident.
                Graphics.Blit(source, destination);
                return;
            }

            int width = Mathf.Max(1, source.width / 2);
            int height = Mathf.Max(1, source.height / 2);
            RenderTextureFormat format = source.format;

            material.SetFloat(ThresholdId, threshold);
            material.SetFloat(KneeId, Mathf.Max(0.0001f, knee));

            RenderTexture bright = RenderTexture.GetTemporary(width, height, 0, format);
            RenderTexture scratch = RenderTexture.GetTemporary(width, height, 0, format);

            bright.filterMode = FilterMode.Bilinear;
            scratch.filterMode = FilterMode.Bilinear;

            Graphics.Blit(source, bright, material, 0);

            // Each round is one horizontal and one vertical pass, and the offset grows so
            // the kernel widens without the sample count doing the same.
            for (int i = 0; i < blurIterations; i++)
            {
                float spread = 1f + i;

                material.SetVector(BlurDirectionId, new Vector4(spread, 0f, 0f, 0f));
                Graphics.Blit(bright, scratch, material, 1);

                material.SetVector(BlurDirectionId, new Vector4(0f, spread, 0f, 0f));
                Graphics.Blit(scratch, bright, material, 1);
            }

            material.SetTexture(BloomTexId, bright);
            material.SetFloat(BloomIntensityId, bloomIntensity);
            material.SetFloat(ExposureId, exposure);
            material.SetFloat(ContrastId, contrast);
            material.SetFloat(SaturationId, saturation);
            material.SetVector(ColourFilterId, colourFilter);
            material.SetFloat(VignetteIntensityId, vignetteIntensity);
            material.SetFloat(VignetteSmoothnessId, Mathf.Max(0.01f, vignetteSmoothness));
            material.SetFloat(GrainIntensityId, grainIntensity);
            material.SetFloat(GrainSeedId, animateGrain ? Time.unscaledTime * 13.7f % 100f : 0f);

            Graphics.Blit(source, destination, material, 2);

            RenderTexture.ReleaseTemporary(scratch);
            RenderTexture.ReleaseTemporary(bright);
        }
    }
}
