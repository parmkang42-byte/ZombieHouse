using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// The drift in a scope held by hand.
    ///
    /// Without it the rifle's scope is a laser: the reticle sits dead still wherever it is put,
    /// and a headshot at thirty metres is exactly as easy as one at three. That flattens the
    /// only weapon in the game that is supposed to reward patience into the one that needs
    /// none. So while scoped the view breathes, and a long shot is taken at the bottom of a
    /// breath rather than whenever you like.
    ///
    /// TWO MOTIONS, BOTH SMOOTH. A slow figure-eight -- the rise and fall of a breath, crossed
    /// with a sideways lean -- and a faint tremor over it. The tremor is Perlin noise, not
    /// random numbers per frame: a view that jumps a little every frame reads as the game
    /// stuttering, while one that wanders reads as a hand. Test Scope Sway bounds the
    /// per-frame step for exactly that reason.
    ///
    /// SLIGHT, AND MEASURED AS SLIGHT. At the scope's 18-degree field of view the drift is
    /// about a zombie's head-width at thirty metres and a fraction of it up close, so it
    /// decides long shots and barely touches short ones. The test asserts both ends: enough
    /// to notice, not enough to fight.
    ///
    /// NO BIAS. The figure-eight is centred and the noise has its mean taken out, so over a
    /// breath the aim averages back to where it was put. A sway that pulled the shot one way
    /// would not be harder, just wrong.
    ///
    /// Offsets only. The sway is added to the camera on top of the player's aim by MouseLook
    /// and never written into the aim itself, so it cannot accumulate into a drift, and
    /// lowering the scope returns the view to exactly where the player was pointing.
    /// </summary>
    [System.Serializable]
    public class ScopeSway
    {
        [Tooltip("Size of the breathing figure-eight, in degrees.")]
        [SerializeField] private float breathDegrees = 0.34f;

        [Tooltip("Seconds for one full breath.")]
        [SerializeField] private float breathPeriod = 3.6f;

        [Tooltip("Size of the hand tremor over the breath, in degrees.")]
        [SerializeField] private float tremorDegrees = 0.07f;

        [Tooltip("How fast the tremor wanders. Low enough to drift, never to vibrate.")]
        [SerializeField] private float tremorRate = 1.8f;

        [Tooltip("Roughly how long the sway takes to settle in when scoping, and out when not.")]
        [SerializeField] private float settleSeconds = 0.35f;

        private float _time;
        private float _weight;

        /// <summary>Current offset in degrees: x is pitch, y is yaw.</summary>
        public Vector2 Offset { get; private set; }

        /// <summary>
        /// Advances the sway one step. Eases in while scoped and out when not, so raising the
        /// scope does not jolt the view to somewhere mid-breath.
        /// </summary>
        public Vector2 Tick(float deltaTime, bool scoped)
        {
            if (deltaTime <= 0f) return Offset;

            _time += deltaTime;

            float settle = Mathf.Max(0.01f, settleSeconds);
            _weight = Mathf.MoveTowards(_weight, scoped ? 1f : 0f, deltaTime / settle);
            float eased = _weight * _weight * (3f - 2f * _weight);

            if (eased <= 0f)
            {
                Offset = Vector2.zero;
                return Offset;
            }

            float phase = _time * Mathf.PI * 2f / Mathf.Max(0.1f, breathPeriod);

            // A 1:2 Lissajous: the breath rises and falls twice for every lean to one side and
            // back, which traces the figure-eight a held rifle actually makes.
            float yaw = Mathf.Sin(phase) * breathDegrees;
            float pitch = Mathf.Sin(phase * 2f) * breathDegrees * 0.6f;

            // Perlin sits between 0 and 1; take the half off so the tremor is centred and the
            // aim does not creep.
            float t = _time * tremorRate;
            yaw += (Mathf.PerlinNoise(t, 17.3f) - 0.5f) * 2f * tremorDegrees;
            pitch += (Mathf.PerlinNoise(41.9f, t) - 0.5f) * 2f * tremorDegrees;

            Offset = new Vector2(pitch, yaw) * eased;
            return Offset;
        }

        /// <summary>Snaps back to rest, for a weapon being put away.</summary>
        public void Reset()
        {
            _weight = 0f;
            Offset = Vector2.zero;
        }
    }
}
