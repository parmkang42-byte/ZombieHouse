using System;
using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Player
{
    /// <summary>
    /// Head torch on F. The fitted cell drains only while the beam is on, so the dark
    /// stops being scenery and becomes something you spend: light the corridor now, or
    /// keep the charge for the room you cannot see into yet.
    ///
    /// The beam builds itself in <see cref="Awake"/> if one was not assigned, so the
    /// torch works however the rig was put together — and so a test can drop the
    /// component on a bare object without standing up the whole player.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        [Header("Beam")]
        [SerializeField] private Light beam;
        [SerializeField] private float range = 36f;
        [SerializeField] private float spotAngle = 52f;
        [SerializeField] private float innerSpotAngle = 24f;
        [SerializeField] private float intensity = 4.8f;
        [SerializeField] private Color beamColour = new Color(0.94f, 0.93f, 0.82f);

        [Header("Battery")]
        [Tooltip("Seconds of light in a fresh cell, with the beam on the whole time.")]
        [SerializeField] private float secondsPerCell = 300f;
        [Tooltip("Spare cells in your pocket at the start of the level.")]
        [SerializeField] private int spareCells = 1;
        [SerializeField] private int maxSpareCells = 4;
        [Tooltip("Seconds spent in the dark swapping a dead cell for a fresh one.")]
        [SerializeField] private float swapSeconds = 1.15f;

        [Header("Dying cell")]
        [Tooltip("Fraction of a cell below which the beam starts to flicker and dim.")]
        [SerializeField] private float lowFraction = 0.18f;
        [SerializeField] private float flickerSpeed = 11f;
        [Tooltip("How dim the beam can drop to on the worst flicker, as a fraction.")]
        [SerializeField] private float flickerFloor = 0.35f;

        private float _charge;
        private bool _on;
        private float _swapRemaining;
        private float _flickerPhase;

        /// <summary>Charge left in the fitted cell, 0–1. What the HUD meter draws.</summary>
        public float BatteryFraction => secondsPerCell <= 0f ? 0f : Mathf.Clamp01(_charge / secondsPerCell);

        /// <summary>
        /// The spotlight this torch drives. Exposed so a test can inspect the thing the
        /// player actually sees rather than hunting for a Light in the rig — the muzzle
        /// flash is a Light too, and it is the one a naive search finds first.
        /// </summary>
        public Light Beam => beam;

        /// <summary>True when the player has asked for light — even mid-flicker.</summary>
        public bool IsOn => _on;
        public bool IsSwapping => _swapRemaining > 0f;
        public int SpareCells => spareCells;
        public bool IsDead => _charge <= 0f;

        /// <summary>Raised when the fitted cell runs out, so audio can sell the moment.</summary>
        public event Action Died;
        public event Action Toggled;
        public event Action Swapped;

        private bool _initialised;

        private void Awake()
        {
            Initialise();
        }

        /// <summary>
        /// Builds the beam and fits a fresh cell. Awake calls it in a running game, and
        /// the editor test calls it by hand — Awake does not run in edit mode, so a test
        /// that only added the component would be inspecting a torch that was never
        /// switched together. Safe to call more than once.
        /// </summary>
        public void Initialise()
        {
            if (_initialised) return;
            _initialised = true;

            _charge = secondsPerCell;
            if (beam == null) beam = BuildBeam();
            _flickerPhase = UnityEngine.Random.Range(0f, 100f);
            ApplyBeam();
        }

        /// <summary>
        /// The spotlight itself. Pixel-lit and shadow-casting on purpose: a vertex-lit
        /// torch does not pick out the edge of a doorway, which is the whole reason to
        /// carry one, and the moving shadows are most of the fright.
        /// </summary>
        private Light BuildBeam()
        {
            var go = new GameObject("FlashlightBeam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.12f, -0.08f, 0.2f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;

            // Everything the world camera can see. Never narrow this to the player layer:
            // a torch that lights nothing but your own hands is the bug this guards.
            light.cullingMask = ~0;
            return light;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// One step of torch time. Split out from <see cref="Update"/> so the headless
        /// test can drain a cell in a few calls instead of standing in the dark for five
        /// minutes.
        /// </summary>
        public void Tick(float deltaTime)
        {
            Initialise();
            if (InputReader.FlashlightPressed) Press();

            if (IsSwapping)
            {
                _swapRemaining -= deltaTime;
                if (_swapRemaining <= 0f)
                {
                    _swapRemaining = 0f;
                    _charge = secondsPerCell;
                    _on = true;
                }

                ApplyBeam();
                return;
            }

            if (_on && _charge > 0f)
            {
                _charge = Mathf.Max(0f, _charge - deltaTime);
                if (_charge <= 0f)
                {
                    _on = false;
                    Died?.Invoke();
                }
            }

            _flickerPhase += deltaTime * flickerSpeed;
            ApplyBeam();
        }

        /// <summary>
        /// What F does. On a live cell it toggles; on a dead one it starts a swap if you
        /// have a spare, so the same key means "give me light" whatever state you are in.
        /// </summary>
        public void Press()
        {
            Initialise();
            if (IsSwapping) return;

            if (_charge <= 0f)
            {
                if (spareCells <= 0) return;
                spareCells--;
                _swapRemaining = swapSeconds;
                Swapped?.Invoke();
                return;
            }

            _on = !_on;
            Toggled?.Invoke();
        }

        /// <summary>
        /// Pockets a cell. Returns how many were taken, so a pickup you have no room for
        /// stays on the floor for when you do — same contract as ammo and medkits.
        /// </summary>
        public int AddBattery(int count)
        {
            int taken = Mathf.Min(count, Mathf.Max(0, maxSpareCells - spareCells));
            spareCells += taken;
            return taken;
        }

        /// <summary>Drives the actual Light. Everything above only moves numbers.</summary>
        private void ApplyBeam()
        {
            if (beam == null) return;

            beam.range = range;
            beam.spotAngle = spotAngle;
            beam.innerSpotAngle = Mathf.Min(innerSpotAngle, spotAngle - 1f);
            beam.color = beamColour;

            bool lit = _on && _charge > 0f && !IsSwapping;
            beam.enabled = lit;
            if (!lit) return;

            // A cell on its way out browns out and stutters rather than simply switching
            // off — you get warning enough to decide whether to spend the spare.
            float health = Mathf.Clamp01(BatteryFraction / Mathf.Max(0.0001f, lowFraction));
            float flicker = 1f;
            if (health < 1f)
            {
                float wobble = (Mathf.PerlinNoise(_flickerPhase, 0.37f) - 0.5f) * 2f;
                flicker = Mathf.Lerp(Mathf.Lerp(flickerFloor, 1f, 0.5f + wobble * 0.5f), 1f, health);
            }

            beam.intensity = intensity * flicker;
        }
    }
}
