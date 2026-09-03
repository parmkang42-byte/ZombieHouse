using System.Collections.Generic;
using UnityEngine;
using ZombieHouse.Core;

namespace ZombieHouse.Fx
{
    /// <summary>
    /// Picks which of a level's lights are going to fail, and keeps the choice honest.
    ///
    /// One component per level rather than a flag on every lamp in six different generators.
    /// The generators are already the most-edited files in the project and none of them
    /// should have to know that a dread effect exists — a light is a light, and which ones
    /// die is a decision about pacing, not about level geometry.
    ///
    /// Two rules do most of the work:
    ///
    /// **Never near the start.** A light failing in the first few seconds lands during the
    /// opening grace, when the player has been promised nothing can touch them, and reads as
    /// a bug rather than as a threat.
    ///
    /// **Never too many.** A quarter is plenty. Every light that fails is a permanent hole
    /// in the level's readability, and past a certain share the player is not frightened,
    /// they are just squinting — the same trap as placing a torch at every junction and
    /// ending up with sixty of them.
    /// </summary>
    public class DreadDirector : MonoBehaviour
    {
        [Tooltip("Share of a level's lights that will fail when approached.")]
        [Range(0f, 1f)] [SerializeField] private float failureShare = 0.25f;

        [Tooltip("No light within this distance of the player's start may be chosen.")]
        [SerializeField] private float safeStartRadius = 16f;

        [Tooltip("Lights dimmer than this are atmosphere rather than illumination — killing "
                 + "one is not felt, so it is a scare spent for nothing.")]
        [SerializeField] private float minimumIntensity = 0.35f;

        /// <summary>How many were armed. Read by the test.</summary>
        public int Armed { get; private set; }

        private void Start() => Install();

        /// <summary>
        /// Arms the chosen lights. Public because Start does not run in edit mode, so a test
        /// that only adds the component would be inspecting something that never ran.
        /// </summary>
        public int Install()
        {
            Armed = 0;

            Vector3 start = transform.position;
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) start = playerObject.transform.position;

            var candidates = new List<Light>();

            foreach (Light light in Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (light == null || !light.enabled) continue;

                // Directional lights are the sun and the moon. Killing one does not darken a
                // room, it darkens the world.
                if (light.type == LightType.Directional) continue;

                if (light.intensity < minimumIntensity) continue;
                if (light.GetComponent<LightFailure>() != null) continue;

                // The torch moves with the player and is the one light they are counting on.
                if (light.GetComponentInParent<Player.Flashlight>() != null) continue;

                if (Vector3.Distance(light.transform.position, start) < safeStartRadius) continue;

                candidates.Add(light);
            }

            // Seeded from the level's own name so a given level fails the same lights every
            // time it is played. Learnable is fine — the scare survives knowing it is coming,
            // because you still have to walk into the dark afterwards — and it means a player
            // describing what happened to them is describing something real.
            var rng = new System.Random(gameObject.scene.name.GetHashCode());

            // Shuffle, then take. Walking the list and rolling per light clusters them
            // wherever FindObjectsByType happened to return neighbours together.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int wanted = Mathf.RoundToInt(candidates.Count * failureShare);

            for (int i = 0; i < wanted && i < candidates.Count; i++)
            {
                candidates[i].gameObject.AddComponent<LightFailure>().Initialise();
                Armed++;
            }

            return Armed;
        }
    }
}
