using UnityEngine;

namespace ZombieHouse.Enemies
{
    /// <summary>
    /// Rolls this walker's type and pushes the numbers into the other components.
    ///
    /// Runs before everything else on the zombie (execution order -30) so that health,
    /// senses, gait and build are all set before anything reads them: ZombieHealth's
    /// Awake copies max health into current, and ZombieAppearance folds the archetype's
    /// build into its own random variation.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class ZombieProfile : MonoBehaviour
    {
        [Tooltip("Leave as None to roll a type from the weighted catalogue.")]
        [SerializeField] private bool forceKind;
        [SerializeField] private ZombieKind kind = ZombieKind.Shambler;

        public ZombieArchetype Archetype { get; private set; }

        /// <summary>
        /// Set immediately before Instantiate to force the next zombie's type. Awake
        /// consumes it, so it only ever applies to one spawn.
        ///
        /// The type has to be decided before Awake — health, senses and build are all read
        /// from it during startup — and a freshly instantiated prefab runs Awake before the
        /// spawner gets a reference back to configure it. This is the seam.
        /// </summary>
        public static ZombieKind? NextKindOverride;

        private void Awake()
        {
            if (NextKindOverride.HasValue)
            {
                Archetype = Find(NextKindOverride.Value);
                NextKindOverride = null;
            }
            else
            {
                Archetype = forceKind ? Find(kind) : ZombieArchetype.PickRandom();
            }

            var health = GetComponent<ZombieHealth>();
            if (health != null) health.ApplyArchetype(Archetype);

            var ai = GetComponent<ZombieAI>();
            if (ai != null) ai.ApplyArchetype(Archetype);
        }

        private static ZombieArchetype Find(ZombieKind wanted)
        {
            foreach (ZombieArchetype archetype in ZombieArchetype.Catalogue)
                if (archetype.Kind == wanted) return archetype;

            return ZombieArchetype.Catalogue[0];
        }
    }
}
