using UnityEngine;
using ZombieHouse.Core;
using ZombieHouse.Enemies;

namespace ZombieHouse.Audio
{
    /// <summary>
    /// How much trouble you are in right now, as a number from 0 to 1.
    ///
    /// This exists so the music can stop being scenery. Every bed in the game loops
    /// identically whether you are creeping down an empty corridor or being closed on by
    /// four jaguars, and that is the single biggest thing wrong with the audio: in a horror
    /// game the score is supposed to be a *signal*, and a signal that never changes carries
    /// no information. <see cref="GameAudio"/> reads this to fade a tension layer in over
    /// the bed.
    ///
    /// **What counts as threat.** Something hunting you nearby, weighted by how near. A
    /// chaser at four metres is the whole meter; the same chaser at thirty is almost
    /// nothing. Investigating counts for less than chasing, because a zombie that has heard
    /// something and is coming to look is a different feeling from one that has seen you —
    /// and the music should be able to tell you which is happening before you can see it.
    ///
    /// **Deliberately not counting the dormant horde.** A level holds forty-odd zombies and
    /// most of them are asleep. Counting bodies rather than hunters would peg the meter at
    /// maximum from the first second and it would be a level-population readout, not a
    /// tension one.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class ThreatMeter : MonoBehaviour
    {
        [Tooltip("Beyond this, a hunter contributes nothing. Roughly the distance at which "
                 + "you could still get away without being seen.")]
        [SerializeField] private float dangerRadius = 30f;

        [Tooltip("How many close chasers count as maximum threat. Low, because the "
                 + "difference between one and three is the interesting part — past that "
                 + "you are already in trouble and the music has nothing left to say.")]
        [SerializeField] private float huntersForFullThreat = 3.5f;

        [Tooltip("Something that has only heard you is worth this much of one that has seen "
                 + "you. The gap is what lets the score distinguish suspicion from pursuit.")]
        [Range(0f, 1f)] [SerializeField] private float investigateWeight = 0.35f;

        [Tooltip("Seconds between samples. The meter drives a slow crossfade, so polling "
                 + "faster than this buys nothing and costs a scene search each time.")]
        [SerializeField] private float sampleInterval = 0.4f;

        /// <summary>The current reading, 0 to 1. Zero when there is no meter in the scene.</summary>
        public static float Level { get; private set; }

        /// <summary>How many things are actively hunting, for the HUD or a test to read.</summary>
        public static int Hunters { get; private set; }

        private Transform _player;
        private float _nextSample;

        private void OnEnable()
        {
            Level = 0f;
            Hunters = 0;
        }

        private void OnDisable()
        {
            // A destroyed meter must not leave the music stuck at full tension for ever.
            Level = 0f;
            Hunters = 0;
        }

        private void Update()
        {
            if (!GameManager.GameplayActive) return;
            if (Time.time < _nextSample) return;

            _nextSample = Time.time + Mathf.Max(0.05f, sampleInterval);
            Level = Sample();
        }

        /// <summary>
        /// One reading. Public so a test can drive it without a scene, a player, or frames.
        /// </summary>
        public float Sample()
        {
            if (_player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) _player = playerObject.transform;
                if (_player == null) return 0f;
            }

            // A scene search at 2.5 Hz rather than a registry on ZombieAI. Forty objects
            // twice a second is nothing, and it keeps this feature entirely out of the
            // enemy code, which is the part with the most tests hanging off it.
            ZombieAI[] all = Object.FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);

            float total = 0f;
            int hunting = 0;
            Vector3 here = _player.position;

            foreach (ZombieAI ai in all)
            {
                if (ai == null) continue;

                float weight = WeightOf(ai.State);
                if (weight <= 0f) continue;

                float distance = Vector3.Distance(here, ai.transform.position);
                if (distance > dangerRadius) continue;

                hunting++;

                // Squared falloff: something at half the radius is worth four times
                // something at the edge, which is how proximity actually feels.
                float nearness = 1f - Mathf.Clamp01(distance / dangerRadius);
                total += weight * nearness * nearness;
            }

            Hunters = hunting;
            return Mathf.Clamp01(total / Mathf.Max(0.01f, huntersForFullThreat));
        }

        private float WeightOf(ZombieState state)
        {
            switch (state)
            {
                case ZombieState.Attack:
                case ZombieState.Chase:
                    return 1f;

                case ZombieState.Investigate:
                    return investigateWeight;

                default:
                    return 0f;
            }
        }
    }
}
