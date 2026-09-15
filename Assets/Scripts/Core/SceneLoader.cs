using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZombieHouse.Core
{
    /// <summary>
    /// Loads a scene behind a loading screen whose bar shades in as the load goes.
    ///
    /// Every scene load in the game goes through here: starting the game from the Boot scene,
    /// and restarting a level. See LoadProgress for why the bar is split into a loading half and
    /// a building half, and why the split is learned rather than chosen.
    /// </summary>
    public static class SceneLoader
    {
        private const string SharePrefix = "ZombieHouse.LoadShare.";

        /// <summary>Whether a load is already under way. A second request during one is ignored.</summary>
        public static bool IsLoading => LoadingScreen.Current != null;

        public static void Load(string sceneName)
        {
            if (IsLoading || string.IsNullOrEmpty(sceneName)) return;
            LoadingScreen.Begin(sceneName);
        }

        public static void Reload()
        {
            Load(SceneManager.GetActiveScene().name);
        }

        /// <summary>"Level1_House" as the screen shows it: "LEVEL 1 · HOUSE".</summary>
        public static string DisplayName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return "LOADING";

            Match m = Regex.Match(sceneName, @"^Level(\d+)_(.+)$");
            if (!m.Success) return sceneName.Replace('_', ' ').ToUpperInvariant();

            // Split CamelCase so a two-word level name reads as two words.
            string words = Regex.Replace(m.Groups[2].Value, "(?<=[a-z])(?=[A-Z])", " ");
            return "LEVEL " + m.Groups[1].Value + " · " + words.ToUpperInvariant();
        }

        /// <summary>The loading half's remembered share of the bar for a scene.</summary>
        public static float RememberedShare(string sceneName)
        {
            return PlayerPrefs.GetFloat(SharePrefix + sceneName, LoadProgress.DefaultAssetShare);
        }

        internal static void Remember(string sceneName, float share)
        {
            PlayerPrefs.SetFloat(SharePrefix + sceneName, share);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The overlay itself. Built in code like the HUD, kept alive across the scene change, and
    /// gone once the new level has built and the bar has filled.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Current { get; private set; }

        // Deep red while it is early, bone as it nears full: the bar shades as it fills.
        private static readonly Color Early = new Color(0.36f, 0.05f, 0.04f);
        private static readonly Color Late = new Color(0.78f, 0.73f, 0.62f);

        private CanvasGroup _group;
        private RectTransform _fill;
        private Image _fillImage;
        private RectTransform _edge;
        private Text _percent;
        private Text _phase;
        private float _shown;

        /// <summary>The fraction the bar is currently drawn at. For the test.</summary>
        public float Shown => _shown;

        public string PhaseText => _phase != null ? _phase.text : string.Empty;
        public string PercentText => _percent != null ? _percent.text : string.Empty;

        internal static void Begin(string sceneName)
        {
            LoadingScreen screen = Create(SceneLoader.DisplayName(sceneName));
            DontDestroyOnLoad(screen.gameObject);
            Current = screen;
            screen.StartCoroutine(screen.Run(sceneName));
        }

        /// <summary>Builds the overlay without starting a load. Public so a test can draw one.</summary>
        public static LoadingScreen Create(string title)
        {
            var root = new GameObject("Loading Screen");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;   // above the HUD, which sits at 100

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var screen = root.AddComponent<LoadingScreen>();
            screen._group = root.AddComponent<CanvasGroup>();
            screen._group.blocksRaycasts = false;

            // Opaque, so a half-built level is never on screen underneath.
            Rect(root.transform, "Backdrop", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<Image>().color = Color.black;

            Text heading = Label(root.transform, "Title", title, 46, new Color(0.86f, 0.84f, 0.80f),
                                 new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold;

            RectTransform track = Rect(root.transform, "Track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(-460f, -10f), new Vector2(460f, 10f));
            track.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.11f);

            screen._fill = Rect(track, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            screen._fillImage = screen._fill.gameObject.AddComponent<Image>();
            screen._fillImage.color = Early;

            // A thin bright leading edge, so a bar that is not moving still reads as a bar that
            // has got somewhere rather than as a coloured box.
            screen._edge = Rect(track, "Edge", Vector2.zero, new Vector2(0f, 1f), new Vector2(-2f, -3f), new Vector2(2f, 3f));
            screen._edge.gameObject.AddComponent<Image>().color = new Color(1f, 0.93f, 0.80f, 0.85f);

            screen._percent = Label(root.transform, "Percent", "0%", 26, new Color(0.62f, 0.60f, 0.56f),
                                    new Vector2(0.5f, 0.5f), new Vector2(505f, 0f), TextAnchor.MiddleLeft);
            screen._phase = Label(root.transform, "Phase", "Loading", 26, new Color(0.55f, 0.53f, 0.50f),
                                  new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), TextAnchor.MiddleCenter);

            screen.Draw(0f);
            return screen;
        }

        /// <summary>Draws the bar at a fraction, with a phase label. Immediate; no easing.</summary>
        public void Show(float fraction, string phase)
        {
            if (_phase != null && phase != null) _phase.text = phase;
            Draw(fraction);
        }

        private IEnumerator Run(string sceneName)
        {
            var progress = new LoadProgress(SceneLoader.RememberedShare(sceneName));

            Show(0f, "Loading");

            // One frame with the overlay up before anything blocks, or the first thing the
            // player sees is the old scene freezing.
            yield return null;

            float started = Time.realtimeSinceStartup;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError("[Loading] '" + sceneName + "' is not in the build settings.");
                Finish();
                yield break;
            }

            op.allowSceneActivation = false;

            // Unity reports reading a scene as 0 to 0.9 and holds at 0.9 until activation.
            while (op.progress < 0.9f)
            {
                progress.ReportLoading(op.progress / 0.9f);
                Ease(progress.Value);
                yield return null;
            }

            progress.BeginBuilding();
            float loaded = Time.realtimeSinceStartup;

            // Draw the bar full to the end of the loading half, with the label that explains the
            // pause, and let that frame reach the screen before the level builds and blocks.
            Show(progress.Value, "Building the level");
            yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Awake and Start have run: the generators have built and the NavMesh has baked.
            yield return null;

            float built = Time.realtimeSinceStartup;
            progress.Complete();
            SceneLoader.Remember(sceneName,
                LoadProgress.Learn(progress.AssetShare, loaded - started, built - loaded));

            // Fill the rest visibly rather than snapping, then hold full for a beat and fade.
            float t = 0f;
            float from = _shown;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                Show(Mathf.Lerp(from, 1f, t / 0.2f), "Ready");
                yield return null;
            }
            Show(1f, "Ready");

            float hold = 0f;
            while (hold < 0.25f) { hold += Time.unscaledDeltaTime; yield return null; }

            float fade = 0f;
            while (fade < 0.35f)
            {
                fade += Time.unscaledDeltaTime;
                if (_group != null) _group.alpha = 1f - fade / 0.35f;
                yield return null;
            }

            Finish();
        }

        /// <summary>Animates toward the true progress without ever running ahead of it or back.</summary>
        private void Ease(float target)
        {
            float next = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 1.5f);
            Draw(Mathf.Max(_shown, Mathf.Min(next, target)));
        }

        private void Draw(float fraction)
        {
            _shown = Mathf.Clamp01(fraction);
            if (_fill != null) _fill.anchorMax = new Vector2(_shown, 1f);
            if (_edge != null)
            {
                _edge.anchorMin = new Vector2(_shown, 0f);
                _edge.anchorMax = new Vector2(_shown, 1f);
            }
            if (_fillImage != null) _fillImage.color = Color.Lerp(Early, Late, _shown);
            if (_percent != null) _percent.text = Mathf.RoundToInt(_shown * 100f) + "%";
        }

        private void Finish()
        {
            if (Current == this) Current = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private static Text Label(Transform parent, string name, string text, int size, Color colour,
                                  Vector2 anchor, Vector2 offset, TextAnchor alignment)
        {
            RectTransform rect = Rect(parent, name, anchor, anchor, offset - new Vector2(420f, 30f),
                                      offset + new Vector2(420f, 30f));
            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.fontSize = size;
            label.color = colour;
            label.alignment = alignment;
            label.font = Font;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        private static Font _font;

        private static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return _font;
            }
        }
    }
}
