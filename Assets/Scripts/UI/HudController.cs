using UnityEngine;
using UnityEngine.UI;
using ZombieHouse.Combat;
using ZombieHouse.Core;
using ZombieHouse.Player;

namespace ZombieHouse.UI
{
    /// <summary>
    /// The heads-up display, built as a uGUI canvas in code so the scene carries no
    /// prefab wiring. Replaces the Stage 1 IMGUI HUD: this one animates, layers properly,
    /// and does not allocate every frame.
    ///
    /// Everything is driven from state the gameplay already exposes — nothing here
    /// pushes values into the HUD, the HUD pulls them.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("References (found automatically if left empty)")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Weapon weapon;
        [SerializeField] private Transform cameraTransform;

        [Header("Crosshair")]
        [SerializeField] private float crosshairBaseGap = 7f;
        [SerializeField] private float crosshairLength = 11f;
        [SerializeField] private float crosshairThickness = 2.5f;
        [SerializeField] private float spreadToPixels = 55f;
        [SerializeField] private float crosshairLerpSpeed = 14f;

        [Header("Feedback")]
        [SerializeField] private float hitMarkerSeconds = 0.22f;
        [SerializeField] private float damageIndicatorSeconds = 1.4f;
        [SerializeField] private float healthBarLerpSpeed = 6f;

        [Header("Colours")]
        [SerializeField] private Color accent = new Color(0.85f, 0.87f, 0.9f, 0.9f);
        [SerializeField] private Color danger = new Color(0.95f, 0.3f, 0.28f);
        [SerializeField] private Color good = new Color(0.42f, 0.82f, 0.45f);

        private const int IndicatorCount = 4;

        private Canvas _canvas;
        private Image _vignette;
        private Image[] _crosshairBars;   // left, right, up, down
        private Image _crosshairDot;
        private Image[] _hitMarker;
        private Image _healthFill;
        private RectTransform _healthFillRect;
        private Text _healthLabel;
        private Text _ammoLabel;
        private Text _weaponLabel;
        private Player.Flashlight _flashlight;
        private Image _batteryFill;
        private RectTransform _batteryFillRect;
        private Text _batteryLabel;
        private float _batteryBarWidth = 150f;
        private WeaponSwitcher _switcher;
        private Text _objectiveLabel;
        private Text _killsLabel;
        private Image _objectiveBacking;
        private Text _interactPrompt;

        private Image[] _damageIndicators;
        private float[] _damageIndicatorTimes;
        private int _nextIndicator;

        private GameObject _scopeRoot;
        private RectTransform _scopeCircle;
        private RectTransform _scopeFillLeft;
        private RectTransform _scopeFillRight;
        private RectTransform _canvasRect;
        private Player.MouseLook _mouseLook;

        private GameObject _overlayRoot;
        private Image _overlayBacking;
        private Text _overlayTitle;
        private Text _overlaySubtitle;

        private float _hitMarkerUntil;
        private bool _lastHitCritical;
        private float _displayedHealth = 1f;
        private float _displayedGap;
        private float _healthBarWidth = 300f;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (weapon == null) weapon = FindAnyObjectByType<Weapon>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            _flashlight = FindAnyObjectByType<Player.Flashlight>();
            _switcher = FindAnyObjectByType<WeaponSwitcher>();

            _displayedGap = crosshairBaseGap;
            BuildCanvas();
        }

        private void OnEnable()
        {
            if (weapon != null) weapon.HitConfirmed += OnHitConfirmed;
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.HitConfirmed -= OnHitConfirmed;
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
        }

        // ---- construction ---------------------------------------------------

        private static Font UiFont
        {
            get
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                return font;
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("HUD Canvas");
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Transform root = canvasObject.transform;

            BuildVignette(root);
            BuildCrosshair(root);
            BuildDamageIndicators(root);
            BuildHealth(root);
            BuildBattery(root);
            BuildAmmo(root);
            BuildObjective(root);
            BuildInteractPrompt(root);
            BuildScope(root);
            BuildOverlay(root);

            _canvasRect = (RectTransform)root;
        }

        /// <summary>
        /// The sniper picture: a black surround with a clear circle, a ring, and a mil-dot
        /// reticle. The circle is kept square and sized to the screen height every frame,
        /// with black bars filling the sides — stretching one image across the screen
        /// would squash the circle into an ellipse on any non-square display.
        /// </summary>
        private void BuildScope(Transform parent)
        {
            _scopeRoot = new GameObject("Scope", typeof(RectTransform));
            _scopeRoot.transform.SetParent(parent, false);
            Stretch((RectTransform)_scopeRoot.transform);

            // Side fills pinned to each edge and stretched vertically; only their width
            // changes, which keeps them correct at any aspect ratio.
            Image left = CreateImage("ScopeFillLeft", _scopeRoot.transform, Color.black);
            _scopeFillLeft = left.rectTransform;
            _scopeFillLeft.anchorMin = new Vector2(0f, 0f);
            _scopeFillLeft.anchorMax = new Vector2(0f, 1f);
            _scopeFillLeft.pivot = new Vector2(0f, 0.5f);
            _scopeFillLeft.anchoredPosition = Vector2.zero;
            _scopeFillLeft.sizeDelta = new Vector2(0f, 0f);

            Image right = CreateImage("ScopeFillRight", _scopeRoot.transform, Color.black);
            _scopeFillRight = right.rectTransform;
            _scopeFillRight.anchorMin = new Vector2(1f, 0f);
            _scopeFillRight.anchorMax = new Vector2(1f, 1f);
            _scopeFillRight.pivot = new Vector2(1f, 0.5f);
            _scopeFillRight.anchoredPosition = Vector2.zero;
            _scopeFillRight.sizeDelta = new Vector2(0f, 0f);

            Image circle = CreateImage("ScopeCircle", _scopeRoot.transform, Color.white);
            circle.sprite = Fx.ProtoTextures.ScopeMask;
            Centre(circle.rectTransform, Vector2.zero, new Vector2(1080f, 1080f));
            _scopeCircle = circle.rectTransform;

            // Reticle: crosshair, plus mil-dots down the vertical for holdover.
            var reticle = new GameObject("Reticle", typeof(RectTransform)).transform;
            reticle.SetParent(_scopeRoot.transform, false);
            Centre((RectTransform)reticle, Vector2.zero, new Vector2(10f, 10f));

            // Three layers, because a reticle has to stay readable against both a bright
            // window and a black corridor:
            //   heavy posts  dark, for structure at the edges
            //   crosshair    light, drawn on a dark shadow line so it reads either way
            //   centre       red, marking the exact point of aim
            Color post = new Color(0.04f, 0.04f, 0.04f, 0.95f);
            Color shadow = new Color(0f, 0f, 0f, 0.55f);
            Color cross = new Color(0.94f, 0.95f, 0.97f, 0.95f);
            Color aimPoint = new Color(0.95f, 0.22f, 0.18f, 0.98f);

            // Heavy outer posts.
            CreateReticleBar(reticle, new Vector2(-260f, 0f), new Vector2(250f, 3f), post);
            CreateReticleBar(reticle, new Vector2(260f, 0f), new Vector2(250f, 3f), post);
            CreateReticleBar(reticle, new Vector2(0f, 260f), new Vector2(3f, 250f), post);
            CreateReticleBar(reticle, new Vector2(0f, -260f), new Vector2(3f, 250f), post);

            // The targeting cross itself: continuous lines that meet in the middle, each
            // sitting on a slightly thicker dark line so it never disappears into a pale
            // or a dark background.
            CreateReticleBar(reticle, Vector2.zero, new Vector2(300f, 3.5f), shadow);
            CreateReticleBar(reticle, Vector2.zero, new Vector2(3.5f, 300f), shadow);
            CreateReticleBar(reticle, Vector2.zero, new Vector2(300f, 1.6f), cross);
            CreateReticleBar(reticle, Vector2.zero, new Vector2(1.6f, 300f), cross);

            // Ticks along the cross, for judging range and lead.
            for (int i = 1; i <= 4; i++)
            {
                float offset = 40f * i;
                CreateReticleBar(reticle, new Vector2(-offset, 0f), new Vector2(1.6f, 11f), cross);
                CreateReticleBar(reticle, new Vector2(offset, 0f), new Vector2(1.6f, 11f), cross);
                CreateReticleBar(reticle, new Vector2(0f, -offset), new Vector2(11f, 1.6f), cross);
            }

            // Point of aim: a red dot on a dark surround so it stays visible on anything.
            CreateReticleBar(reticle, Vector2.zero, new Vector2(7f, 7f), shadow);
            CreateReticleBar(reticle, Vector2.zero, new Vector2(4f, 4f), aimPoint);

            _scopeRoot.SetActive(false);
        }

        private void CreateReticleBar(Transform parent, Vector2 position, Vector2 size, Color colour)
        {
            Image bar = CreateImage("ReticleBar", parent, colour);
            Centre(bar.rectTransform, position, size);
        }

        private void BuildVignette(Transform parent)
        {
            _vignette = CreateImage("Vignette", parent, new Color(0.7f, 0f, 0f, 0f));
            Stretch(_vignette.rectTransform);
            _vignette.raycastTarget = false;
        }

        private void BuildCrosshair(Transform parent)
        {
            var container = new GameObject("Crosshair", typeof(RectTransform)).transform;
            container.SetParent(parent, false);
            Centre((RectTransform)container, Vector2.zero, new Vector2(200f, 200f));

            _crosshairBars = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                _crosshairBars[i] = CreateImage("Bar" + i, container, accent);
                _crosshairBars[i].raycastTarget = false;
            }

            _crosshairDot = CreateImage("Dot", container, new Color(1f, 1f, 1f, 0.55f));
            Centre(_crosshairDot.rectTransform, Vector2.zero, new Vector2(2.5f, 2.5f));
            _crosshairDot.raycastTarget = false;

            _hitMarker = new Image[4];
            for (int i = 0; i < 4; i++)
            {
                _hitMarker[i] = CreateImage("HitMarker" + i, container, Color.white);
                Centre(_hitMarker[i].rectTransform, Vector2.zero, new Vector2(11f, 2.5f));
                _hitMarker[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 90f);
                _hitMarker[i].enabled = false;
                _hitMarker[i].raycastTarget = false;
            }
        }

        private void BuildDamageIndicators(Transform parent)
        {
            var container = new GameObject("DamageIndicators", typeof(RectTransform)).transform;
            container.SetParent(parent, false);
            Centre((RectTransform)container, Vector2.zero, new Vector2(10f, 10f));

            _damageIndicators = new Image[IndicatorCount];
            _damageIndicatorTimes = new float[IndicatorCount];

            for (int i = 0; i < IndicatorCount; i++)
            {
                // A bar parked above centre; rotating its parent pivot swings it around
                // the screen to point at whatever hit you.
                var pivot = new GameObject("Pivot" + i, typeof(RectTransform)).transform;
                pivot.SetParent(container, false);
                Centre((RectTransform)pivot, Vector2.zero, new Vector2(10f, 10f));

                Image bar = CreateImage("Bar", pivot, danger);
                Centre(bar.rectTransform, new Vector2(0f, 150f), new Vector2(90f, 7f));
                bar.raycastTarget = false;
                bar.enabled = false;

                _damageIndicators[i] = bar;
                _damageIndicatorTimes[i] = float.NegativeInfinity;
            }
        }

        private void BuildHealth(Transform parent)
        {
            var backing = CreateImage("HealthBacking", parent, new Color(0f, 0f, 0f, 0.55f));
            Anchor(backing.rectTransform, new Vector2(0f, 0f), new Vector2(46f, 54f),
                   new Vector2(_healthBarWidth + 6f, 24f));

            _healthFill = CreateImage("HealthFill", parent, good);
            _healthFillRect = _healthFill.rectTransform;
            Anchor(_healthFillRect, new Vector2(0f, 0f), new Vector2(49f, 57f),
                   new Vector2(_healthBarWidth, 18f));
            _healthFillRect.pivot = new Vector2(0f, 0f);

            _healthLabel = CreateText("HealthLabel", parent, 20, TextAnchor.LowerLeft, accent);
            Anchor(_healthLabel.rectTransform, new Vector2(0f, 0f), new Vector2(48f, 84f),
                   new Vector2(300f, 28f));
        }

        /// <summary>
        /// The torch meter, sitting directly above the health bar because it is the same
        /// kind of thing: something you are spending that you cannot get back except by
        /// finding more. Narrower than the health bar so the two never read as one.
        /// </summary>
        private void BuildBattery(Transform parent)
        {
            var backing = CreateImage("BatteryBacking", parent, new Color(0f, 0f, 0f, 0.55f));
            Anchor(backing.rectTransform, new Vector2(0f, 0f), new Vector2(46f, 112f),
                   new Vector2(_batteryBarWidth + 6f, 16f));

            _batteryFill = CreateImage("BatteryFill", parent, accent);
            _batteryFillRect = _batteryFill.rectTransform;
            Anchor(_batteryFillRect, new Vector2(0f, 0f), new Vector2(49f, 115f),
                   new Vector2(_batteryBarWidth, 10f));
            _batteryFillRect.pivot = new Vector2(0f, 0f);

            _batteryLabel = CreateText("BatteryLabel", parent, 15, TextAnchor.LowerLeft,
                                       new Color(0.7f, 0.7f, 0.72f, 0.85f));
            Anchor(_batteryLabel.rectTransform, new Vector2(0f, 0f), new Vector2(206f, 108f),
                   new Vector2(300f, 22f));
        }

        private void BuildAmmo(Transform parent)
        {
            _ammoLabel = CreateText("Ammo", parent, 44, TextAnchor.LowerRight, accent);
            Anchor(_ammoLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 74f),
                   new Vector2(360f, 56f));
            _ammoLabel.rectTransform.pivot = new Vector2(1f, 0f);

            _weaponLabel = CreateText("WeaponName", parent, 16, TextAnchor.LowerRight,
                                      new Color(0.7f, 0.7f, 0.72f, 0.8f));
            Anchor(_weaponLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-48f, 50f),
                   new Vector2(360f, 24f));
            _weaponLabel.rectTransform.pivot = new Vector2(1f, 0f);
        }

        private void BuildObjective(Transform parent)
        {
            _objectiveBacking = CreateImage("ObjectiveBacking", parent, new Color(0f, 0f, 0f, 0.42f));
            Anchor(_objectiveBacking.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f),
                   new Vector2(640f, 44f));
            _objectiveBacking.rectTransform.pivot = new Vector2(0f, 1f);

            _objectiveLabel = CreateText("Objective", parent, 21, TextAnchor.MiddleLeft, accent);
            Anchor(_objectiveLabel.rectTransform, new Vector2(0f, 1f), new Vector2(58f, -40f),
                   new Vector2(620f, 44f));
            _objectiveLabel.rectTransform.pivot = new Vector2(0f, 1f);

            _killsLabel = CreateText("Kills", parent, 16, TextAnchor.UpperLeft,
                                     new Color(0.65f, 0.65f, 0.68f, 0.85f));
            Anchor(_killsLabel.rectTransform, new Vector2(0f, 1f), new Vector2(58f, -92f),
                   new Vector2(400f, 24f));
            _killsLabel.rectTransform.pivot = new Vector2(0f, 1f);
        }

        /// <summary>
        /// "E — HELP THEM UP", under the crosshair. Only ever on screen when pressing E
        /// would actually do something, so it never becomes wallpaper.
        /// </summary>
        private void BuildInteractPrompt(Transform parent)
        {
            _interactPrompt = CreateText("InteractPrompt", parent, 22, TextAnchor.MiddleCenter, accent);
            Anchor(_interactPrompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f),
                   new Vector2(620f, 30f));
            _interactPrompt.enabled = false;
        }

        private void BuildOverlay(Transform parent)
        {
            _overlayRoot = new GameObject("Overlay", typeof(RectTransform));
            _overlayRoot.transform.SetParent(parent, false);
            Stretch((RectTransform)_overlayRoot.transform);

            _overlayBacking = CreateImage("Backing", _overlayRoot.transform, new Color(0f, 0f, 0f, 0.75f));
            Stretch(_overlayBacking.rectTransform);
            _overlayBacking.raycastTarget = false;

            _overlayTitle = CreateText("Title", _overlayRoot.transform, 76, TextAnchor.MiddleCenter, Color.white);
            Centre(_overlayTitle.rectTransform, new Vector2(0f, 60f), new Vector2(1200f, 110f));

            _overlaySubtitle = CreateText("Subtitle", _overlayRoot.transform, 24, TextAnchor.MiddleCenter,
                                          new Color(0.82f, 0.82f, 0.85f));
            Centre(_overlaySubtitle.rectTransform, new Vector2(0f, -20f), new Vector2(1200f, 60f));

            _overlayRoot.SetActive(false);
        }

        // ---- per-frame ------------------------------------------------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            FollowActiveWeapon();
            UpdateScope();
            UpdateCrosshair(dt);
            UpdateHealth(dt);
            UpdateBattery();
            UpdateAmmo();
            UpdateObjective();
            UpdateInteractPrompt();
            UpdateDamageIndicators();
            UpdateOverlay();
        }

        private void UpdateCrosshair(float dt)
        {
            // Aiming means you are using the iron sights, so the painted crosshair goes
            // away — otherwise the two compete and neither is trusted.
            bool playing = GameManager.GameplayActive && !(weapon != null && weapon.IsAiming);

            float targetGap = crosshairBaseGap;
            if (weapon != null) targetGap += weapon.CurrentSpread * spreadToPixels;
            _displayedGap = Mathf.Lerp(_displayedGap, targetGap, crosshairLerpSpeed * dt);

            Color colour = accent;
            if (weapon != null && weapon.IsReloading) colour = new Color(1f, 0.78f, 0.32f, 0.85f);
            else if (weapon != null && weapon.AmmoInMagazine == 0) colour = danger;

            float gap = _displayedGap;
            float half = crosshairThickness * 0.5f;

            SetBar(_crosshairBars[0], new Vector2(-gap - crosshairLength * 0.5f, 0f),
                   new Vector2(crosshairLength, crosshairThickness), colour, playing);
            SetBar(_crosshairBars[1], new Vector2(gap + crosshairLength * 0.5f, 0f),
                   new Vector2(crosshairLength, crosshairThickness), colour, playing);
            SetBar(_crosshairBars[2], new Vector2(0f, gap + crosshairLength * 0.5f),
                   new Vector2(crosshairThickness, crosshairLength), colour, playing);
            SetBar(_crosshairBars[3], new Vector2(0f, -gap - crosshairLength * 0.5f),
                   new Vector2(crosshairThickness, crosshairLength), colour, playing);

            _crosshairDot.enabled = playing;

            bool showMarker = Time.time < _hitMarkerUntil;
            Color markerColour = _lastHitCritical ? new Color(1f, 0.45f, 0.2f) : Color.white;
            for (int i = 0; i < _hitMarker.Length; i++)
            {
                _hitMarker[i].enabled = showMarker && playing;
                if (!showMarker) continue;

                float t = Mathf.InverseLerp(_hitMarkerUntil - hitMarkerSeconds, _hitMarkerUntil, Time.time);
                markerColour.a = 1f - t;
                _hitMarker[i].color = markerColour;

                // Marker springs outward slightly as it fades.
                float radius = 14f + t * 6f;
                float angle = (45f + i * 90f) * Mathf.Deg2Rad;
                _hitMarker[i].rectTransform.anchoredPosition =
                    new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }

            // Unused, but keeps the intent obvious if thickness is ever animated.
            _ = half;
        }

        private void SetBar(Image bar, Vector2 position, Vector2 size, Color colour, bool visible)
        {
            bar.enabled = visible;
            bar.color = colour;
            Centre(bar.rectTransform, position, size);
        }

        private void UpdateHealth(float dt)
        {
            if (playerHealth == null) return;

            float target = playerHealth.Normalized;
            _displayedHealth = Mathf.Lerp(_displayedHealth, target, healthBarLerpSpeed * dt);

            _healthFillRect.sizeDelta = new Vector2(_healthBarWidth * Mathf.Clamp01(_displayedHealth),
                                                    _healthFillRect.sizeDelta.y);
            _healthFill.color = Color.Lerp(danger, good, target);
            _healthLabel.text = "HEALTH  " + Mathf.CeilToInt(playerHealth.Current);

            // Red wash: sharp on a hit, a steady low throb when badly hurt.
            float lowHealth = 1f - Mathf.Clamp01(target / 0.35f);
            float throb = lowHealth > 0f ? (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f)) : 0f;
            float intensity = Mathf.Max(playerHealth.HitFlash * 0.5f, lowHealth * 0.28f * throb);

            Color vignetteColour = _vignette.color;
            vignetteColour.a = intensity;
            _vignette.color = vignetteColour;
        }

        private void UpdateAmmo()
        {
            if (weapon == null) return;

            _ammoLabel.text = weapon.IsReloading
                ? "RELOADING"
                : weapon.AmmoInMagazine + " / " + weapon.ReserveAmmo;

            _ammoLabel.color = !weapon.IsReloading && weapon.AmmoInMagazine == 0 ? danger : accent;
            _weaponLabel.text = weapon.WeaponName.ToUpperInvariant();
        }

        /// <summary>
        /// Draws the fitted cell, not the spares. A dying cell turns the bar red and the
        /// spare count is what tells you whether that matters.
        /// </summary>
        private void UpdateBattery()
        {
            if (_batteryFill == null) return;

            // No torch in the scene (an old scene, or a test rig): hide the meter rather
            // than drawing an empty one that reads as a flat battery.
            bool present = _flashlight != null;
            _batteryFill.enabled = present;
            _batteryLabel.enabled = present;
            if (!present) return;

            float fraction = _flashlight.BatteryFraction;
            _batteryFillRect.sizeDelta = new Vector2(_batteryBarWidth * fraction,
                                                     _batteryFillRect.sizeDelta.y);

            bool dying = fraction <= 0.18f;
            Color lit = _flashlight.IsOn ? accent : new Color(0.45f, 0.45f, 0.47f, 0.75f);
            _batteryFill.color = dying ? danger : lit;

            int spares = _flashlight.SpareCells;
            string state = _flashlight.IsSwapping ? "SWAPPING"
                         : _flashlight.IsDead ? (spares > 0 ? "DEAD — F TO SWAP" : "DEAD")
                         : Mathf.RoundToInt(fraction * 100f) + "%";

            _batteryLabel.text = "TORCH " + state + (spares > 0 ? "   ·   " + spares + " SPARE" : "");
            _batteryLabel.color = _flashlight.IsDead && spares == 0
                ? danger
                : new Color(0.7f, 0.7f, 0.72f, 0.85f);
        }

        private void UpdateObjective()
        {
            var game = GameManager.Instance;
            if (game == null) return;

            // Two things hold the door. Name whichever one is still outstanding, and
            // name the survivors first — they are the one you can miss entirely.
            if (game.ExitUnlocked)
            {
                _objectiveLabel.text = "THE WAY OUT IS OPEN   ·   everyone is accounted for";
                _objectiveLabel.color = good;
            }
            else if (game.SurvivorsRemaining > 0 && game.ZombiesNeededForExit <= 0)
            {
                _objectiveLabel.text = "Find the survivors   ·   " + game.SurvivorsRemaining +
                                       (game.SurvivorsRemaining == 1 ? " still out there" : " still out there");
                _objectiveLabel.color = danger;
            }
            else if (game.SurvivorsRemaining > 0)
            {
                _objectiveLabel.text = "Clear the level   ·   " + game.ZombiesNeededForExit +
                                       " more, and " + game.SurvivorsRemaining + " survivor(s) to find";
                _objectiveLabel.color = accent;
            }
            else if (game.RequiresMotor && !game.MotorPowered && game.ZombiesNeededForExit <= 0)
            {
                _objectiveLabel.text = game.CarryingPowerCell
                    ? "Carry the cell to the motor by the door"
                    : "Find the power cell   ·   the door motor is dead without it";
                _objectiveLabel.color = game.CarryingPowerCell ? good : danger;
            }
            else
            {
                _objectiveLabel.text = "Clear the level   ·   " + game.ZombiesNeededForExit +
                                       " more before the door will open";
                _objectiveLabel.color = accent;
            }

            _killsLabel.text = "Killed: " + game.ZombiesKilled + " / " + game.ZombiesTotal +
                               "   ·   still standing: " + game.ZombiesAlive +
                               (game.SurvivorsTotal > 0
                                   ? "   ·   survivors: " + game.SurvivorsRescued + " / " + game.SurvivorsTotal
                                   : string.Empty) +
                               (game.RequiresMotor
                                   ? "   ·   motor: " + (game.MotorPowered ? "RUNNING"
                                       : game.CarryingPowerCell ? "cell in hand" : "no power")
                                   : string.Empty);
        }

        private void UpdateInteractPrompt()
        {
            if (_interactPrompt == null) return;

            bool show = GameManager.GameplayActive && Level.InteractPrompt.Active;
            _interactPrompt.enabled = show;
            if (show) _interactPrompt.text = "SPACE   ·   " + Level.InteractPrompt.Label;
        }

        private void UpdateDamageIndicators()
        {
            for (int i = 0; i < _damageIndicators.Length; i++)
            {
                float age = Time.time - _damageIndicatorTimes[i];
                if (age > damageIndicatorSeconds)
                {
                    _damageIndicators[i].enabled = false;
                    continue;
                }

                _damageIndicators[i].enabled = GameManager.GameplayActive;

                Color colour = danger;
                colour.a = 1f - age / damageIndicatorSeconds;
                _damageIndicators[i].color = colour;
            }
        }

        private void UpdateOverlay()
        {
            var game = GameManager.Instance;
            if (game == null) return;

            bool show = game.State != GameState.Playing;
            if (_overlayRoot.activeSelf != show) _overlayRoot.SetActive(show);
            if (!show) return;

            switch (game.State)
            {
                case GameState.Paused:
                    _overlayTitle.text = "PAUSED";
                    _overlayTitle.color = Color.white;
                    _overlaySubtitle.text = "Esc to resume";
                    break;

                case GameState.Won:
                    _overlayTitle.text = "EXTRACTED";
                    _overlayTitle.color = good;
                    _overlaySubtitle.text = "House cleared — " + game.ZombiesKilled +
                                            " down.    Enter to play again";
                    break;

                case GameState.Lost:
                    _overlayTitle.text = "YOU DIED";
                    _overlayTitle.color = danger;
                    _overlaySubtitle.text = game.ZombiesKilled +
                                            " killed before they got you.    Enter to try again";
                    break;
            }
        }

        // ---- events ---------------------------------------------------------

        /// <summary>
        /// Shows the scope picture while looking through the rifle's sight, and slows the
        /// mouse to match the magnification — at an 18 degree field of view, unchanged
        /// sensitivity makes the rifle unusable at range.
        /// </summary>
        private void UpdateScope()
        {
            if (_scopeRoot == null) return;

            bool scoped = weapon != null && weapon.IsScoped && GameManager.GameplayActive;
            if (_scopeRoot.activeSelf != scoped) _scopeRoot.SetActive(scoped);

            if (_mouseLook == null && cameraTransform != null)
                _mouseLook = cameraTransform.GetComponent<Player.MouseLook>();

            if (_mouseLook != null)
                _mouseLook.SensitivityMultiplier = scoped ? 0.32f : 1f;

            if (!scoped || _canvasRect == null) return;

            // Keep the glass circular whatever the aspect ratio, and let the side fills
            // cover whatever is left over.
            float height = _canvasRect.rect.height;
            float width = _canvasRect.rect.width;

            _scopeCircle.sizeDelta = new Vector2(height, height);

            float sideWidth = Mathf.Max(0f, (width - height) * 0.5f);
            _scopeFillLeft.sizeDelta = new Vector2(sideWidth, 0f);
            _scopeFillRight.sizeDelta = new Vector2(sideWidth, 0f);
        }

        /// <summary>
        /// Rebinds to whichever gun is drawn. The hit marker hangs off the weapon's own
        /// event, so swapping weapons has to move that subscription across or the marker
        /// silently stops working on the second gun.
        /// </summary>
        private void FollowActiveWeapon()
        {
            if (_switcher == null || _switcher.Current == null) return;
            if (ReferenceEquals(_switcher.Current, weapon)) return;

            if (weapon != null) weapon.HitConfirmed -= OnHitConfirmed;
            weapon = _switcher.Current;
            weapon.HitConfirmed += OnHitConfirmed;
        }

        private void OnHitConfirmed(bool critical)
        {
            _hitMarkerUntil = Time.time + hitMarkerSeconds;
            _lastHitCritical = critical;
        }

        private void OnPlayerDamaged(float amount)
        {
            if (playerHealth == null || cameraTransform == null) return;

            Vector3 fromAttacker = -playerHealth.LastHitDirection;
            if (fromAttacker.sqrMagnitude < 0.001f) return;

            Vector3 local = cameraTransform.InverseTransformDirection(fromAttacker);
            float angle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

            int index = _nextIndicator;
            _nextIndicator = (_nextIndicator + 1) % _damageIndicators.Length;

            _damageIndicatorTimes[index] = Time.time;
            _damageIndicators[index].transform.parent.localRotation = Quaternion.Euler(0f, 0f, -angle);
            _damageIndicators[index].enabled = true;
        }

        // ---- uGUI helpers ---------------------------------------------------

        private static Image CreateImage(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, int size, TextAnchor anchor, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = colour;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Centre(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
