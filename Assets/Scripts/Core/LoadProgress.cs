using UnityEngine;

namespace ZombieHouse.Core
{
    /// <summary>
    /// How far a level load has got, as a fraction the loading bar can show honestly.
    ///
    /// A LOAD HERE HAS TWO HALVES, AND ONLY ONE OF THEM CAN BE WATCHED. First Unity reads the
    /// scene off disk, which it reports progress for. Then the scene activates and the level
    /// builds itself -- every generator runs in Awake and the NavMesh bakes -- and all of that
    /// happens inside one blocking frame, during which nothing can draw. A bar driven only by
    /// Unity's progress would race to full and then sit there while the house was built, which
    /// is exactly backwards: the bar would say "done" during the longest wait.
    ///
    /// So the bar is split. The loading half fills with Unity's real progress; the building half
    /// is shown as a labelled pause. And the split is not a guess: it is how long each half
    /// actually took the last time this level loaded, remembered between runs. If building takes
    /// three quarters of the time, the bar stops at a quarter, and the length of that pause is
    /// proportional to what is left -- which is the thing the bar is for.
    ///
    /// Monotonic by construction. A bar that ever moves backwards is lying about one of the two
    /// positions it has shown, and Test Loading checks it never does.
    /// </summary>
    public sealed class LoadProgress
    {
        /// <summary>The loading half's share of the bar before a level has ever been timed.</summary>
        public const float DefaultAssetShare = 0.35f;

        // Neither half may claim the whole bar. One freak load -- a disk spinning up, a cold
        // shader cache -- should not teach the bar that building takes no time at all.
        public const float MinShare = 0.1f;
        public const float MaxShare = 0.9f;

        public LoadProgress(float assetShare)
        {
            AssetShare = Mathf.Clamp(assetShare, MinShare, MaxShare);
        }

        /// <summary>The part of the bar that loading the scene file owns.</summary>
        public float AssetShare { get; }

        /// <summary>What the bar should show, 0 to 1. Never decreases.</summary>
        public float Value { get; private set; }

        public bool Building { get; private set; }
        public bool Done { get; private set; }

        /// <summary>Unity's own 0-1 progress for reading the scene. Ignored once building starts.</summary>
        public void ReportLoading(float fraction)
        {
            if (Building || Done) return;
            Raise(AssetShare * Mathf.Clamp01(fraction));
        }

        /// <summary>The scene is read and about to activate: the loading half is full.</summary>
        public void BeginBuilding()
        {
            if (Done) return;
            Raise(AssetShare);
            Building = true;
        }

        /// <summary>The level has built and is ready to play.</summary>
        public void Complete()
        {
            Building = false;
            Done = true;
            Value = 1f;
        }

        private void Raise(float value)
        {
            // Until the level reports itself built, the bar may not claim any of the building
            // half. That is the honesty rule: full means playable, not merely read from disk.
            float ceiling = Done ? 1f : AssetShare;
            Value = Mathf.Max(Value, Mathf.Min(value, ceiling));
        }

        /// <summary>
        /// The share the loading half should have next time, given how this load went.
        ///
        /// Halfway between the old share and what was just measured, so the bar settles on a
        /// level's real proportions over a couple of loads without being thrown by one odd one.
        /// </summary>
        public static float Learn(float previousShare, float loadingSeconds, float buildingSeconds)
        {
            float total = loadingSeconds + buildingSeconds;
            if (total <= 1e-4f || loadingSeconds < 0f || buildingSeconds < 0f)
                return Mathf.Clamp(previousShare, MinShare, MaxShare);

            float measured = Mathf.Clamp(loadingSeconds / total, MinShare, MaxShare);
            return Mathf.Clamp(Mathf.Lerp(previousShare, measured, 0.5f), MinShare, MaxShare);
        }
    }
}
