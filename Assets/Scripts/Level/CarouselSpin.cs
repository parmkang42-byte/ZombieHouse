using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Level
{
    /// <summary>
    /// Turns the carousel.
    ///
    /// Slowly, and never quite steadily. A ride turning at a constant rate reads as a
    /// working machine, which is the opposite of what this level wants — the park has been
    /// shut for forty years and nothing should be *working*. What it should be is turning,
    /// which is a different and much worse thing: something is driving it, or the wind is,
    /// or nobody knows, and either way it was already turning before you arrived.
    ///
    /// **Only collider-free geometry is attached to this.** The deck the player walks on and
    /// the centre column stay bolted down, and that is not a compromise but a requirement:
    /// the NavMesh is baked once at level start, so a rotating collider would spin under a
    /// navigation surface that does not move with it. The poles and the canopy are already
    /// decorations, they carry the whole visual read, and none of them touches physics.
    /// </summary>
    public class CarouselSpin : MonoBehaviour
    {
        [Tooltip("Degrees per second at its steadiest. A fairground carousel runs about 4 "
                 + "rpm; this is deliberately slower than that.")]
        [SerializeField] private float degreesPerSecond = 7f;

        [Tooltip("How much the rate wanders, as a fraction. Zero is a machine; this is not "
                 + "a machine any more.")]
        [Range(0f, 1f)] [SerializeField] private float wander = 0.35f;

        [Tooltip("Seconds for one full wander cycle — how long it takes to drift from its "
                 + "slowest back to its fastest.")]
        [SerializeField] private float wanderPeriod = 23f;

        [Tooltip("Which way it turns, in local space. Y for a carousel, X for a ferris "
                 + "wheel — the wheel stands upright and rolls about its hub.")]
        [SerializeField] private Vector3 axis = Vector3.up;

        /// <summary>
        /// Sets the rate from a period instead of a rate, because that is how anyone
        /// actually specifies a ride: "one turn every four seconds", not "ninety degrees a
        /// second". Called by the generator.
        /// </summary>
        public void ConfigureTurn(float secondsPerRevolution, Vector3 turnAxis, float drift)
        {
            degreesPerSecond = 360f / Mathf.Max(0.05f, secondsPerRevolution);
            axis = turnAxis;
            wander = Mathf.Clamp01(drift);
        }

        /// <summary>Seconds for one full revolution at the steady rate. For the test.</summary>
        public float SecondsPerRevolution =>
            degreesPerSecond <= 0f ? 0f : 360f / degreesPerSecond;

        private float _phase;

        /// <summary>Total degrees turned. Read by the test, which cannot watch it spin.</summary>
        public float TurnedDegrees { get; private set; }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Advances the spin. Public and taking its own delta so a test can turn it in edit
        /// mode, where Time does not advance and an Update-driven rotation sits at zero
        /// forever — which would let a completely broken carousel pass a green test.
        /// </summary>
        public void Tick(float deltaTime)
        {
            _phase += deltaTime;

            // A slow sine on the rate. Not random per frame: randomness at frame rate is
            // jitter, and jitter reads as a bug. A drift over twenty seconds reads as a
            // mechanism with something wrong in it.
            float drift = 1f + Mathf.Sin(_phase / Mathf.Max(0.1f, wanderPeriod) * Mathf.PI * 2f) * wander;
            float step = degreesPerSecond * drift * deltaTime;

            transform.Rotate(axis.normalized * step, Space.Self);
            TurnedDegrees += step;
        }
    }
}
