using UnityEngine;
using UnityEngine.UI;
using ZombieHouse.Core;
using ZombieHouse.Enemies;

namespace ZombieHouse.UI
{
    /// <summary>
    /// A health bar floating over the boss's head.
    ///
    /// World-space rather than pinned to the top of the screen, and that is a deliberate
    /// choice rather than the easy one. A screen-space boss bar is the convention, but it
    /// tells the player where the boss's health is without telling them where the *boss* is —
    /// and in a game this dark, in levels this open, knowing something is at forty percent
    /// while having no idea which way to face is the wrong kind of tension. A bar over its
    /// head is also a light source in the dark: you can lose the thing itself in fog and
    /// still see the bar drifting behind a tent, which is worse and better at once.
    ///
    /// **It only exists once the fight has started.** Before that it would give away where
    /// the boss is standing, and the whole design of <see cref="LevelBoss"/> is that you walk
    /// past it in the dark without knowing.
    /// </summary>
    [RequireComponent(typeof(ZombieHealth))]
    public class BossHealthBar : MonoBehaviour
    {
        [Tooltip("Metres above the boss's own height to float. Scaled by the boss's size, "
                 + "so a 3.2x giant carries it higher than a 1.7x one.")]
        [SerializeField] private float clearance = 0.9f;

        [Tooltip("Width of the bar in metres at 1x scale.")]
        [SerializeField] private float width = 2.6f;

        [Tooltip("How fast the bar chases the real value. Slower than instant, so a burst "
                 + "from the gatling reads as a slide rather than a jump.")]
        [SerializeField] private float drainSpeed = 0.55f;

        private ZombieHealth _health;
        private Canvas _canvas;
        private Image _fill;
        private Image _chase;
        private Transform _camera;

        private float _shown = 1f;
        private float _chased = 1f;
        private float _height = 2f;

        private void Awake() => Initialise();

        /// <summary>
        /// Builds the bar. Public because Awake does not run in edit mode, so a test that
        /// only calls AddComponent would be inspecting a component with no canvas.
        /// </summary>
        public void Initialise()
        {
            if (_canvas != null) return;

            _health = GetComponent<ZombieHealth>();
            _height = MeasureHeight();

            var canvasObject = new GameObject("BossHealthBar");
            canvasObject.transform.SetParent(transform, false);
            canvasObject.transform.localPosition = Vector3.up * (_height + clearance);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rect = _canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, width * 0.09f);
            rect.localScale = Vector3.one;

            // A dark backing, so the bar reads against fog and against a lit midway alike.
            var backing = NewImage(rect, "Backing", new Color(0.05f, 0.04f, 0.05f, 0.82f));
            backing.rectTransform.sizeDelta = rect.sizeDelta;

            // The chase bar: a paler ghost that lags behind the real one, so a big hit shows
            // you how big it was after the fact. Cheap, and it is most of what makes a boss
            // bar feel responsive rather than merely accurate.
            _chase = NewImage(rect, "Chase", new Color(0.72f, 0.24f, 0.20f, 0.85f));
            _fill = NewImage(rect, "Fill", new Color(0.86f, 0.16f, 0.14f, 1f));

            SetWidth(_chase, 1f);
            SetWidth(_fill, 1f);

            _canvas.enabled = false;
        }

        /// <summary>Real rendered height of this boss, so the bar clears its actual head.</summary>
        private float MeasureHeight()
        {
            float low = float.MaxValue, high = float.MinValue;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                low = Mathf.Min(low, renderer.bounds.min.y);
                high = Mathf.Max(high, renderer.bounds.max.y);
            }

            return high <= low ? 2f : high - low;
        }

        private static Image NewImage(RectTransform parent, string name, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = colour;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            return image;
        }

        private void SetWidth(Image image, float fraction)
        {
            if (image == null || _canvas == null) return;

            RectTransform parent = _canvas.GetComponent<RectTransform>();
            image.rectTransform.sizeDelta =
                new Vector2(parent.sizeDelta.x * Mathf.Clamp01(fraction), parent.sizeDelta.y);
        }

        private void LateUpdate()
        {
            if (_canvas == null || _health == null) return;

            // Nothing before the fight starts. A bar hanging over a dormant boss in the dark
            // would point straight at the one thing the level is trying not to show you.
            bool fighting = LevelBoss.Engaged && !LevelBoss.Defeated && _health.IsAlive;

            if (_canvas.enabled != fighting) _canvas.enabled = fighting;
            if (!fighting) return;

            float fraction = _health.Max <= 0f ? 0f : _health.Current / _health.Max;
            Tick(Time.deltaTime, fraction);
            FaceCamera();
        }

        /// <summary>
        /// Advances the bar. Separated from LateUpdate and taking its own delta so a test can
        /// drive it in edit mode, where Time does not advance.
        /// </summary>
        public void Tick(float deltaTime, float normalised)
        {
            _shown = Mathf.Clamp01(normalised);
            _chased = Mathf.MoveTowards(_chased, _shown, deltaTime * drainSpeed);

            SetWidth(_fill, _shown);
            SetWidth(_chase, Mathf.Max(_chased, _shown));
        }

        private void FaceCamera()
        {
            if (_camera == null)
            {
                Camera main = Camera.main;
                if (main == null) return;
                _camera = main.transform;
            }

            // Billboard about Y only. A bar that tilts to face a camera looking down at it
            // reads as a floating card rather than as part of the world.
            Vector3 away = _canvas.transform.position - _camera.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f) return;

            _canvas.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
    }
}
