using UnityEngine;
using UnityEngine.UI;

namespace ZombieHouse.Level
{
    /// <summary>
    /// A television left on in the room the player wakes up in, running the emergency broadcast.
    ///
    /// WHY. The house opens with no explanation: a dark room, noises downstairs, and then the
    /// first walker. This is the explanation, delivered the way it would actually reach
    /// somebody -- a news channel nobody switched off, still running its loop to an empty room.
    /// It is the first thing in view from where the player starts, so the story arrives before
    /// the first zombie does and nothing has to stop the game to tell it.
    ///
    /// BUILT IN CODE, LIKE EVERYTHING ELSE. The screen is a world-space uGUI canvas: the text
    /// stays crisp at any distance and the whole thing needs no imported art. The live footage
    /// box is generated static with emergency lights flickering through it.
    ///
    /// COSMETIC. No part of it carries a collider. The NavMesh bakes from colliders, and the
    /// house's navigation counts are the canary for anything added to a level -- a television
    /// must not change where a walker can walk. Test Breaking News checks for colliders, and
    /// Verify Level checks the counts.
    /// </summary>
    public class BreakingNewsTV : MonoBehaviour
    {
        public const string Headline = "THE DEAD ARE WALKING — STAY INDOORS";

        public const string Ticker =
            "AUTHORITIES CONFIRM OUTBREAK SPREADING ACROSS THE COUNTY   •   BITES CONFIRMED AS " +
            "SOURCE OF INFECTION — DO NOT APPROACH THE INFECTED   •   HOSPITALS OVERWHELMED, " +
            "EMERGENCY SERVICES NOT RESPONDING   •   LOCK YOUR DOORS AND STAY AWAY FROM WINDOWS   " +
            "•   EVACUATION POINTS OPENING AT DAWN — AVOID MAIN ROADS   •   POWER " +
            "OUTAGES REPORTED IN SEVERAL AREAS   •   IF SOMEONE YOU LOVE HAS BEEN BITTEN, " +
            "DO NOT LET THEM IN   •   ";

        // Canvas pixels. The screen is 0.82 x 0.46 m, so a canvas pixel is one millimetre.
        private const float CanvasWidth = 820f;
        private const float CanvasHeight = 460f;
        private const float TickerSpeed = 95f;

        [SerializeField] private RectTransform ticker;
        [SerializeField] private Text tickerText;
        [SerializeField] private RawImage footage;
        [SerializeField] private Image sirenRed;
        [SerializeField] private Image sirenBlue;
        [SerializeField] private CanvasGroup screen;
        [SerializeField] private Light glow;

        private float _time;
        private float _nextGlitch = 6f;
        private float _glitchLeft;
        private float _tickerWidth;
        private float _glowBase;

        /// <summary>How far the ticker has scrolled, in canvas pixels. For the test.</summary>
        public float TickerOffset => ticker != null ? ticker.anchoredPosition.x : 0f;

        public Light Glow => glow;

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// One frame of broadcast. Public because Update does not run in edit mode, and a test
        /// that wants to see the ticker move has to move it.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _time += deltaTime;

            ScrollTicker(deltaTime);

            // Static: shift the noise texture about, which reads as the picture breaking up.
            if (footage != null)
            {
                footage.uvRect = new Rect(Random.value, Random.value, 1f, 1f);
            }

            // Emergency lights through the footage, alternating.
            float siren = Mathf.Repeat(_time * 1.6f, 1f);
            // Faint: at full strength the two halves read as a red-and-blue split screen rather than
            // lights flashing through the footage, which is what the capture showed.
            if (sirenRed != null) sirenRed.color = new Color(0.9f, 0.08f, 0.06f, siren < 0.5f ? 0.20f : 0.02f);
            if (sirenBlue != null) sirenBlue.color = new Color(0.15f, 0.3f, 1f, siren >= 0.5f ? 0.20f : 0.02f);

            // Now and then the signal drops for a moment. An empty room lit by a television that
            // keeps losing its picture is worse than one that does not.
            _nextGlitch -= deltaTime;
            if (_nextGlitch <= 0f)
            {
                _glitchLeft = 0.16f;
                _nextGlitch = Random.Range(6f, 12f);
            }

            float visible = 1f;
            if (_glitchLeft > 0f)
            {
                _glitchLeft -= deltaTime;
                visible = Random.value < 0.5f ? 0.15f : 0.85f;
            }
            if (screen != null) screen.alpha = visible;

            if (glow != null)
            {
                if (_glowBase <= 0f) _glowBase = glow.intensity;
                float flicker = 0.82f + 0.36f * Mathf.PerlinNoise(_time * 5.5f, 3.1f);
                glow.intensity = _glowBase * flicker * visible;
            }
        }

        private void ScrollTicker(float deltaTime)
        {
            if (ticker == null) return;

            if (_tickerWidth <= 0f)
                _tickerWidth = tickerText != null && tickerText.preferredWidth > 1f
                    ? tickerText.preferredWidth
                    : Ticker.Length * 14f;

            float x = ticker.anchoredPosition.x - TickerSpeed * deltaTime;

            // Wrap once the whole message has passed off the left edge, so it loops forever
            // with the start coming back in from the right.
            if (x < -_tickerWidth) x += _tickerWidth + CanvasWidth;
            ticker.anchoredPosition = new Vector2(x, ticker.anchoredPosition.y);
        }

        // ------------------------------------------------------------------ building

        /// <summary>
        /// Builds the whole set under `parent`: body, stand, screen and glow. The set faces its
        /// own +Z; the caller turns it to face the room. Every part is collider-free.
        /// </summary>
        public static BreakingNewsTV Build(Transform parent, Material plastic)
        {
            var root = new GameObject("Television");
            root.transform.SetParent(parent, false);

            Cosmetic(root.transform, "TvStandBase", new Vector3(0f, 0.01f, -0.02f), new Vector3(0.30f, 0.02f, 0.18f), plastic);
            Cosmetic(root.transform, "TvStandNeck", new Vector3(0f, 0.05f, -0.03f), new Vector3(0.06f, 0.07f, 0.04f), plastic);
            Cosmetic(root.transform, "TvBody", new Vector3(0f, 0.35f, 0f), new Vector3(0.88f, 0.52f, 0.05f), plastic);

            var tv = root.AddComponent<BreakingNewsTV>();
            tv.BuildScreen(root.transform);
            tv.BuildGlow(root.transform);
            return tv;
        }

        private void BuildScreen(Transform root)
        {
            var canvasObject = new GameObject("Screen", typeof(RectTransform));
            var rect = (RectTransform)canvasObject.transform;
            rect.SetParent(root, false);

            // Just proud of the bezel, and turned round: a canvas is read looking along its own
            // +Z, and people look at a television from in front of it.
            rect.localPosition = new Vector3(0f, 0.35f, 0.0262f);
            rect.localRotation = Quaternion.Euler(0f, 180f, 0f);
            rect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            rect.localScale = Vector3.one * 0.001f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // World-space text is rasterised at its font size in canvas units, which at a
            // millimetre a pixel is a blur up close. Rendering glyphs at four times that keeps
            // the headline sharp when the player walks right up to the set.
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 4f;
            screen = canvasObject.AddComponent<CanvasGroup>();
            screen.blocksRaycasts = false;
            screen.interactable = false;

            Panel(rect, "Backdrop", 0f, 0f, CanvasWidth, CanvasHeight, new Color(0.02f, 0.03f, 0.08f));

            // --- the footage: static with emergency lights through it -------------------
            footage = RawPanel(rect, "Footage", 20f, 110f, 780f, 300f);
            footage.texture = StaticTexture();
            footage.color = new Color(0.55f, 0.55f, 0.58f);
            RectTransform footageRect = footage.rectTransform;

            sirenRed = Panel(footageRect, "SirenRed", 0f, 0f, 390f, 300f, new Color(0.9f, 0.05f, 0.05f, 0.3f));
            sirenBlue = Panel(footageRect, "SirenBlue", 390f, 0f, 390f, 300f, new Color(0.1f, 0.25f, 1f, 0.3f));

            // Starts clear of the LIVE badge, which sits over this corner of the footage. The first
            // in-engine capture had the badge on top of the caption, reading "L[LIVE]OUNTY HOSPITAL".
            Text footageCaption = Label(footageRect, "FootageCaption", "COUNTY HOSPITAL", 22, Color.white,
                                        new Vector2(118f, 250f), new Vector2(500f, 30f), TextAnchor.MiddleLeft);
            footageCaption.fontStyle = FontStyle.Bold;

            Image live = Panel(rect, "LiveBug", 36f, 364f, 86f, 34f, new Color(0.85f, 0.05f, 0.05f));
            Text liveText = Label(live.rectTransform, "LiveText", "LIVE", 24, Color.white,
                                  Vector2.zero, new Vector2(86f, 34f), TextAnchor.MiddleCenter);
            liveText.fontStyle = FontStyle.Bold;

            Label(rect, "Channel", "CH 7 NEWS", 22, new Color(1f, 1f, 1f, 0.8f),
                  new Vector2(606f, 364f), new Vector2(180f, 34f), TextAnchor.MiddleRight);

            // --- the lower third ---------------------------------------------------------
            Image banner = Panel(rect, "Banner", 0f, 64f, 330f, 46f, new Color(0.80f, 0.04f, 0.04f));
            Text breaking = Label(banner.rectTransform, "BreakingNews", "BREAKING NEWS", 34, Color.white,
                                  Vector2.zero, new Vector2(330f, 46f), TextAnchor.MiddleCenter);
            breaking.fontStyle = FontStyle.Bold;

            Image strap = Panel(rect, "Strap", 330f, 64f, 490f, 46f, new Color(0.93f, 0.92f, 0.88f));
            Text clock = Label(strap.rectTransform, "Clock", "11:48 PM", 22, new Color(0.15f, 0.15f, 0.15f),
                               new Vector2(0f, 0f), new Vector2(476f, 46f), TextAnchor.MiddleRight);
            clock.fontStyle = FontStyle.Bold;

            Image headlineBar = Panel(rect, "HeadlineBar", 0f, 34f, CanvasWidth, 30f, new Color(0.98f, 0.97f, 0.94f));
            Text headline = Label(headlineBar.rectTransform, "Headline", Headline, 22, new Color(0.08f, 0.08f, 0.08f),
                                  new Vector2(14f, 0f), new Vector2(CanvasWidth - 28f, 30f), TextAnchor.MiddleLeft);
            headline.fontStyle = FontStyle.Bold;

            // --- the ticker ----------------------------------------------------------------
            Image strip = Panel(rect, "TickerStrip", 0f, 0f, CanvasWidth, 34f, new Color(0.05f, 0.05f, 0.07f));
            strip.gameObject.AddComponent<RectMask2D>();

            tickerText = Label(strip.rectTransform, "TickerText", Ticker, 22, new Color(1f, 0.85f, 0.25f),
                               new Vector2(CanvasWidth, 0f), new Vector2(4000f, 34f), TextAnchor.MiddleLeft);
            tickerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            ticker = tickerText.rectTransform;
        }

        private void BuildGlow(Transform root)
        {
            var lightObject = new GameObject("ScreenGlow");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.35f, 0.35f);

            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.62f, 0.72f, 1f);
            glow.range = 3.2f;
            glow.intensity = 0.55f;

            // Never casts. It is set dressing, like a pickup's halo, not one of the room's
            // lights -- and a shadow-casting point light is six shadow maps for a flicker.
            glow.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ helpers

        private static void Cosmetic(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
        }

        /// <summary>A panel positioned from the canvas's bottom-left corner, in canvas pixels.</summary>
        private static Image Panel(RectTransform parent, string name, float x, float y, float w, float h, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            var image = go.AddComponent<Image>();
            image.color = colour;
            return image;
        }

        private static RawImage RawPanel(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return go.AddComponent<RawImage>();
        }

        private static Text Label(RectTransform parent, string name, string text, int size, Color colour,
                                  Vector2 position, Vector2 extent, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = extent;
            var label = go.AddComponent<Text>();
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = alignment;
            label.font = UiFont;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static Font _font;

        private static Font UiFont
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }

        private static Texture2D _static;

        private static Texture2D StaticTexture()
        {
            if (_static != null) return _static;

            const int W = 96, H = 54;
            _static = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "BroadcastStatic",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave
            };

            // Seeded so a rebuild of the level does not differ for no reason.
            var rng = new System.Random(1147);
            var pixels = new Color32[W * H];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte v = (byte)(28 + rng.Next(0, 110));
                pixels[i] = new Color32(v, v, v, 255);
            }
            _static.SetPixels32(pixels);
            _static.Apply();
            return _static;
        }
    }
}
