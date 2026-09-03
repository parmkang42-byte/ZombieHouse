using UnityEngine;
using ZombieHouse.Audio;
using ZombieHouse.Core;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// A light that stutters and dies as you walk up to it.
    ///
    /// Darkness is the fear mechanic in this game — the torch, the thinned-out lamps, the
    /// dark between the lights being the level. This takes light away at the exact moment
    /// the player is committing to a room, which does three things at once: it is a sharp
    /// noise and a sudden movement, it makes the space genuinely harder to read, and it
    /// arrives while they are walking forward rather than while they are safe.
    ///
    /// **It is caused by the player, and that is the whole trick.** A lamp that fails on a
    /// timer is weather. A lamp that fails when you approach it feels like it is about you,
    /// and the player cannot tell the difference between "this is scripted" and "something
    /// is in here" — which is the correct amount of doubt.
    ///
    /// **It fails once and stays failed.** A light that comes back is a strobe, and a strobe
    /// is annoying rather than frightening; it also makes the level permanently harder to
    /// read in the way the player has already learned to handle. Losing a light for good is
    /// a change to the map that they have to live with.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFailure : MonoBehaviour
    {
        [Tooltip("How close the player has to come. Short enough that they are inside the "
                 + "room, not passing the doorway.")]
        [SerializeField] private float triggerRange = 7f;

        [Tooltip("How long the stutter lasts before it goes for good.")]
        [SerializeField] private float flickerSeconds = 0.85f;

        private Light _light;
        private Transform _player;
        private float _intensity;
        private float _elapsed = -1f;
        private System.Random _rng;

        /// <summary>True once it has gone dark for good. Read by the test.</summary>
        public bool HasFailed { get; private set; }

        private void Awake() => Initialise();

        /// <summary>
        /// Public because Awake does not run in edit mode, so a test that only calls
        /// AddComponent is otherwise inspecting a half-built object.
        /// </summary>
        public void Initialise()
        {
            if (_light != null) return;

            _light = GetComponent<Light>();
            _intensity = _light != null ? _light.intensity : 0f;
            // Seeded per lamp so each one stutters its own way rather than every light in
            // the level failing in lockstep. Position rather than an instance id: ids are
            // obsolete in this Unity version, and a lamp is uniquely identified by where it
            // hangs anyway — which has the side benefit of being stable across a rebuild.
            Vector3 at = transform.position;
            _rng = new System.Random(Mathf.RoundToInt(at.x * 73f + at.y * 131f + at.z * 197f));
        }

        private void Update()
        {
            if (!GameManager.GameplayActive || _light == null) return;

            if (_elapsed < 0f)
            {
                if (_player == null)
                {
                    var playerObject = GameObject.FindGameObjectWithTag("Player");
                    if (playerObject != null) _player = playerObject.transform;
                    if (_player == null) return;
                }

                if (Vector3.Distance(transform.position, _player.position) > triggerRange) return;

                Trigger();
                return;
            }

            Tick(Time.deltaTime);
        }

        /// <summary>Starts the failure. Public so a test can fire it without a player.</summary>
        public void Trigger()
        {
            if (_elapsed >= 0f) return;

            Initialise();
            _elapsed = 0f;

            // The pop is what makes it a scare rather than a dimming. It also travels, so a
            // light failing behind the player still registers.
            GameAudio.PlayAt(Sfx.LightPop, transform.position, 0.8f);
        }

        /// <summary>
        /// Drives the stutter. Separated from Update and taking its own delta so the whole
        /// failure can be run in edit mode, where Time.time does not advance and a test
        /// driving Update would sit on frame zero forever.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_light == null || HasFailed || _elapsed < 0f) return;

            _elapsed += deltaTime;

            if (_elapsed >= flickerSeconds)
            {
                _light.intensity = 0f;
                _light.enabled = false;
                HasFailed = true;
                return;
            }

            // Uneven, and getting worse. An even flicker reads as a effect; the ragged
            // version reads as a filament actually failing, because that is what dying
            // filaments do — they arc, catch, and arc again, with the gaps getting longer.
            float progress = _elapsed / Mathf.Max(0.01f, flickerSeconds);
            float chance = 0.35f + progress * 0.5f;

            bool dark = _rng.NextDouble() < chance;
            _light.intensity = dark ? _intensity * (float)(_rng.NextDouble() * 0.18f)
                                    : _intensity * (float)(0.75f + _rng.NextDouble() * 0.35f);
        }
    }
}
