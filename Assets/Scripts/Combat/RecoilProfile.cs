using UnityEngine;

namespace ZombieHouse.Combat
{
    /// <summary>
    /// How a firearm kicks. One of these per weapon, because recoil is most of what
    /// separates a hand cannon from a carbine once the damage numbers are on screen.
    ///
    /// Three things here are what make it feel like a gun rather than a screen shake:
    ///
    /// **It climbs.** The first shot from rest is the smallest; each shot fired before
    /// the muzzle has settled kicks harder than the last, up to a cap. Firing in pairs is
    /// controllable, holding the trigger is not.
    ///
    /// **It walks.** The horizontal component is a drifting bias, not fresh noise every
    /// shot, so a burst wanders off in one direction and can be learned and corrected —
    /// where pure randomness can only be endured.
    ///
    /// **Some of it does not come back.** A share of every kick is added to your actual
    /// aim rather than to a decaying offset, so sustained fire genuinely walks the muzzle
    /// up the wall and you have to pull down. That share is the single most important
    /// number here; at zero the gun is a laser with an animation on it.
    /// </summary>
    [System.Serializable]
    public class RecoilProfile
    {
        [Header("Per shot, from rest")]
        [Tooltip("Degrees the view kicks up on the first shot.")]
        public float verticalDegrees = 1.4f;

        [Tooltip("Degrees of sideways drift at full bias.")]
        public float horizontalDegrees = 0.4f;

        [Header("Climb under sustained fire")]
        [Tooltip("Extra kick per shot, as a fraction of the first shot.")]
        public float climbPerShot = 0.3f;

        [Tooltip("Ceiling on the climb, as a multiple of the first shot.")]
        public float maximumClimb = 2.4f;

        [Tooltip("Seconds without firing before the muzzle has settled and the climb resets.")]
        public float settleSeconds = 0.45f;

        [Header("Where it goes")]
        [Tooltip("Share of each kick that becomes real aim instead of a recovering offset. "
                 + "This is what forces you to pull back down.")]
        [Range(0f, 1f)] public float uncorrectedShare = 0.22f;

        [Tooltip("Recoil multiplier while aiming down sights — bracing the weapon.")]
        [Range(0.1f, 1f)] public float aimedMultiplier = 0.72f;

        // Running state. Lives here rather than on the weapon so a weapon holstered
        // mid-burst comes back settled, which is what putting it away would do.
        private float _shotsInBurst;
        private float _lastShotTime = -99f;
        private float _drift;

        /// <summary>
        /// Works out this shot's kick. Returns degrees: x up, y sideways.
        /// </summary>
        public Vector2 NextKick(bool aiming)
        {
            if (Time.time - _lastShotTime > settleSeconds) Reset();
            _lastShotTime = Time.time;

            float climb = Mathf.Min(1f + _shotsInBurst * climbPerShot, Mathf.Max(1f, maximumClimb));
            _shotsInBurst++;

            // The bias wanders rather than resampling: a burst pulls one way, and which
            // way is not knowable in advance but is correctable once it starts.
            _drift = Mathf.Clamp(_drift + Random.Range(-0.75f, 0.75f), -1f, 1f);

            // Nudge it off dead centre on the first shot so a burst never walks straight
            // up in a perfect line, which reads as scripted.
            if (_shotsInBurst <= 1f) _drift = Random.Range(-0.35f, 0.35f);

            float scale = climb * (aiming ? aimedMultiplier : 1f);
            return new Vector2(verticalDegrees * scale, horizontalDegrees * _drift * scale);
        }

        /// <summary>Muzzle settled: the next shot starts from the bottom of the climb again.</summary>
        public void Reset()
        {
            _shotsInBurst = 0f;
            _drift = 0f;
        }

        /// <summary>Shots fired since the muzzle last settled. Exposed for the recoil test.</summary>
        public float ShotsInBurst => _shotsInBurst;
    }
}
